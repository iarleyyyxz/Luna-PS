using System;
using System.Collections.Generic;
using Luna.GPU;
using Luna.Math;

namespace Luna.GPU
{
    public class GPU
    {

        private readonly Queue<uint> commandBuffer = new Queue<uint>();
        private readonly IGPURenderer renderer;

        public ushort[,] vram = new ushort[1024, 512]; // 1MB VRAM (16bpp)
        private byte transferMode = 0;
        private int transferX, transferY, transferWidth, transferHeight;
        private int transferCount = 0;

        // Estados de configuração do GPU
        private int drawAreaLeft = 0, drawAreaTop = 0, drawAreaRight = 0x3FF, drawAreaBottom = 0x1FF;
        private int drawOffsetX = 0, drawOffsetY = 0;
        // Removido: displayAreaX, displayAreaY, displayAreaW, displayAreaH (não usados)
        private bool maskBit = false;

        private uint gp1Control = 0;
        private Action<int, bool> RaiseIRQ; // delegar IRQ para CPU

        public GPU(IGPURenderer rendererBackend)
        {
            renderer = rendererBackend;
            Reset();
        }

        public void Reset()
        {
            // Limpar buffers e estados
            commandBuffer.Clear();
            Array.Clear(vram, 0, vram.Length);
            renderer.Clear();
            // Resetar estados de transferência
            transferMode = 0;
            transferX = transferY = transferWidth = transferHeight = 0;
            transferCount = 0;
        }

        /// <summary>
        /// Prepara o GPU para desenhar, limpando buffers e estados.
        /// Deve ser chamado antes de iniciar uma nova sequência de comandos.
        /// </summary>
        public void PrepareForDrawing()
        {
            Console.WriteLine("[GPU] Preparando GPU para desenho...");
            Reset();
        }

        public void WriteGP0(uint value)
        {
            // Etapa 1: Preparar a transferência para VRAM (GP0 A0h)
            if (transferMode == 1)
            {
                commandBuffer.Enqueue(value);
                if (commandBuffer.Count == 3)
                {
                    ExecuteCommand();     // chama SetupVRAMTransfer(...)
                    commandBuffer.Clear();
                    transferMode = 2;     // passa para etapa de envio de dados
                }
                return;
            }

            // Etapa 2: Enviar dados para a VRAM
            if (transferMode == 2)
            {
                UploadToVRAM(value);
                // UploadToVRAM deve zerar transferMode quando finalizar
                return;
            }

            // Etapa 3: Interpretação normal de comandos GP0
            byte cmd = (byte)(value >> 24);
            if (cmd == 0xA0)
            {
                // Início de uma nova transferência para VRAM
                commandBuffer.Clear();
                commandBuffer.Enqueue(value);
                transferMode = 1;
                return;
            }

            // Comando comum (como triângulo, etc.)
            commandBuffer.Enqueue(value);
            if (IsCommandComplete())
            {
                ExecuteCommand();
                commandBuffer.Clear();
            }
        }


        public void WriteGP1(uint value)
        {
            // Bit 0: Acknowledge IRQ
            if ((value & 0x1) != 0)
            {
                gp1Control &= ~(1u << 24); // clear IRQ enable
                RaiseIRQ?.Invoke(0, false); // clear IRQ0
            }

            // Bit 24: Enable IRQ1
            if ((value & (1u << 24)) != 0)
            {
                gp1Control |= (1u << 24);
            }

            Console.WriteLine($"[GPU] GP1 escrito: 0x{value:X8}");
        }

        public void Render()
        {
            renderer.UpdateVRAMTexture(vram);
            renderer.Present();
        }

        public void ConnectIRQHandler(Action<int, bool> irqCallback)
        {
            RaiseIRQ = irqCallback;
        }

        private void UploadToVRAM(uint data)
        {
            if (transferWidth == 0 || transferHeight == 0)
            {
                Console.WriteLine("[GPU] Erro: tentativa de escrever na VRAM sem tamanho válido.");
                transferMode = 0;
                transferCount = 0;
                return;
            }

            // Cada palavra contém 2 pixels 16bpp
            ushort pixel1 = (ushort)(data & 0xFFFF);
            ushort pixel2 = (ushort)((data >> 16) & 0xFFFF);

            int px = transferX + (transferCount % transferWidth);
            int py = transferY + (transferCount / transferWidth);

            if (px < 1024 && py < 512) vram[px, py] = pixel1;
            px++;
            if (px < 1024 && py < 512) vram[px, py] = pixel2;

            transferCount += 2;

            if (transferCount >= transferWidth * transferHeight)
            {
                Console.WriteLine("[GPU] Transferência para VRAM finalizada");

                // Gera IRQ0 se o bit 24 de gp1Control estiver habilitado
                if ((gp1Control & (1u << 24)) != 0)
                {
                    Console.WriteLine("[GPU] IRQ0 gerado ao finalizar transferência");
                    RaiseIRQ?.Invoke(0, true); // IRQ0
                }

                // Atualiza textura imediatamente após transferência
                renderer.UpdateVRAMTexture(vram);

                transferMode = 0;
                transferCount = 0;
            }
        }


        private bool IsCommandComplete()
        {
            if (commandBuffer.Count == 0) return false;
            byte cmd = (byte)(commandBuffer.Peek() >> 24);
            return cmd switch
            {
                0x20 => commandBuffer.Count >= 4, // Triângulo plano
                0x24 => commandBuffer.Count >= 7, // Triângulo texturizado
                0xA0 => commandBuffer.Count >= 3, // Transferência para VRAM (setup)
                _ => true,
            };
        }

        private void ExecuteCommand()
        {
            uint[] data = commandBuffer.ToArray();
            if (data.Length == 0) return;
            byte cmd = (byte)(data[0] >> 24);

            Console.WriteLine($"[GPU] GP0 cmd=0x{cmd:X2} ({data.Length} words): " + string.Join(", ", ToHexStrings(data)));
            switch (cmd)
            {
                case 0x00:
                    // GP0 00h: NOP
                    break;
                case 0x20:
                    Console.WriteLine("[GPU] DrawFlatTriangle");
                    DrawFlatTriangle(data);
                    break;
                case 0x24:
                    Console.WriteLine("[GPU] DrawTexturedTriangle");
                    DrawTexturedTriangle(data);
                    break;
                case 0x28:
                    Console.WriteLine("[GPU] CopyRectangleVRAMtoVRAM");
                    CopyRectangleVRAMtoVRAM(data);
                    break;
                case 0x2A:
                    Console.WriteLine("[GPU] CopyRectangleVRAMtoCPU (not implemented)");
                    break;
                case 0x2C:
                    Console.WriteLine("[GPU] SetupVRAMTransfer (alias 0xA0)");
                    SetupVRAMTransfer(data);
                    break;
                case 0x80:
                    Console.WriteLine("[GPU] ClearCache (not implemented)");
                    break;
                case 0xA0:
                    Console.WriteLine("[GPU] SetupVRAMTransfer");
                    SetupVRAMTransfer(data);
                    break;
                case 0xC0:
                    Console.WriteLine("[GPU] DrawOpaqueRectangle");
                    DrawOpaqueRectangle(data);
                    break;
                case 0xE0:
                    Console.WriteLine("[GPU] SetTexturePage (not implemented)");
                    break;
                case 0xE1:
                    Console.WriteLine("[GPU] SetDrawingAreaTopLeft");
                    drawAreaLeft = (int)(data[0] & 0x3FF);
                    drawAreaTop = (int)((data[0] >> 10) & 0x3FF);
                    break;
                case 0xE2:
                    Console.WriteLine("[GPU] SetDrawingAreaBottomRight");
                    drawAreaRight = (int)(data[0] & 0x3FF);
                    drawAreaBottom = (int)((data[0] >> 10) & 0x3FF);
                    break;
                case 0xE3:
                    Console.WriteLine("[GPU] SetDrawingOffset");
                    drawOffsetX = (short)(data[0] & 0x7FF); // signed 11 bits
                    drawOffsetY = (short)((data[0] >> 11) & 0x7FF);
                    break;
                case 0xE4:
                    Console.WriteLine("[GPU] MaskBitSetting");
                    maskBit = ((data[0] & 1) != 0);
                    break;
                default:
                    Console.WriteLine($"[GPU] Comando GP0 desconhecido: 0x{cmd:X2}");
                    break;
            }
        }

        // Mantido apenas uma definição de ToHexStrings


        private static IEnumerable<string> ToHexStrings(uint[] data)
        {
            foreach (var d in data)
                yield return $"0x{d:X8}";
        }

        // GP0 C0h: Draw Opaque Monochrome Rectangle
        private void DrawOpaqueRectangle(uint[] data)
        {
            // data[0]: comando + cor
            // data[1]: XY
            // data[2]: WH
            if (data.Length < 3) return;
            ushort color = (ushort)(data[0] & 0xFFFF); // 15bpp
            int x = (int)(data[1] & 0xFFFF);
            int y = (int)((data[1] >> 16) & 0xFFFF);
            int w = (int)(data[2] & 0xFFFF);
            int h = (int)((data[2] >> 16) & 0xFFFF);
            for (int iy = 0; iy < h; iy++)
            for (int ix = 0; ix < w; ix++)
            {
                int px = x + ix;
                int py = y + iy;
                if (px >= 0 && px < 1024 && py >= 0 && py < 512)
                    vram[px, py] = color;
            }
            // Atualiza textura imediatamente após desenhar retângulo
            renderer.UpdateVRAMTexture(vram);
        }


        // --- Métodos auxiliares para comandos GPU ---
        private void CopyRectangleVRAMtoVRAM(uint[] data)
        {
            // GP0 28h: Copy Rectangle VRAM to VRAM
            // data[0]: comando
            // data[1]: src (X,Y)
            // data[2]: dst (X,Y)
            // data[3]: size (W,H)
            if (data.Length < 4) return;
            int srcX = (int)(data[1] & 0x3FF);
            int srcY = (int)((data[1] >> 16) & 0x1FF);
            int dstX = (int)(data[2] & 0x3FF);
            int dstY = (int)((data[2] >> 16) & 0x1FF);
            int w = (int)(data[3] & 0xFFFF);
            int h = (int)((data[3] >> 16) & 0xFFFF);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int sx = srcX + x;
                int sy = srcY + y;
                int dx = dstX + x;
                int dy = dstY + y;
                if (sx < 1024 && sy < 512 && dx < 1024 && dy < 512)
                    vram[dx, dy] = vram[sx, sy];
            }
        }

        private void SetupVRAMTransfer(uint[] data)
        {
            // GP0 A0h: Copy Rectangle from CPU to VRAM
            if (data.Length < 3)
            {
                Console.WriteLine("[GPU] Dados insuficientes para SetupVRAMTransfer");
                transferMode = 0;
                return;
            }
            transferX = (int)(data[1] & 0xFFFF);
            transferY = (int)(data[1] >> 16);
            transferWidth = (int)(data[2] & 0xFFFF);
            transferHeight = (int)(data[2] >> 16);
            transferCount = 0;
            Console.WriteLine($"[GPU] Transferência para VRAM: ({transferX},{transferY}) {transferWidth}x{transferHeight}");
        }

        private void DrawFlatTriangle(uint[] data)
        {
            if (data.Length < 4) return;
            uint color = data[0] & 0x00FFFFFF;
            Vertex2D v0 = ExtractVertex(data[1]);
            Vertex2D v1 = ExtractVertex(data[2]);
            Vertex2D v2 = ExtractVertex(data[3]);
            renderer.DrawFlatTriangle(v0, v1, v2, color);
        }

        private void DrawTexturedTriangle(uint[] data)
        {
            if (data.Length < 7) return;
            uint color = data[0] & 0x00FFFFFF;
            TexVertex v0 = ExtractTexVertex(data[1], data[2]);
            TexVertex v1 = ExtractTexVertex(data[3], data[4]);
            TexVertex v2 = ExtractTexVertex(data[5], data[6]);
            // Assume textura carregada em uma região da VRAM (ex: 256x256 em 0,0)
            int texId = renderer.CreateTextureFromVRAM(vram, 0, 0, 256, 256);
            renderer.DrawTexturedTriangle(v0, v1, v2, texId);
        }

        private Vertex2D ExtractVertex(uint data)
        {
            float x = data & 0xFFFF;
            float y = (data >> 16) & 0xFFFF;
            return new Vertex2D(x, y);
        }

        private TexVertex ExtractTexVertex(uint posData, uint uvData)
        {
            float x = posData & 0xFFFF;
            float y = (posData >> 16) & 0xFFFF;
            float u = uvData & 0xFF;
            float v = (uvData >> 8) & 0xFF;
            return new TexVertex(x, y, u / 256f, v / 256f); // UV normalizado
        }
    }
}

