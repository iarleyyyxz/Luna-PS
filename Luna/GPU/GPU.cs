using System;
using System.Collections.Generic;
using Luna.Math;

public class GPU
{
    private Queue<uint> commandBuffer = new Queue<uint>();
    private IGPURenderer renderer;

    private ushort[,] vram = new ushort[1024, 512]; // 1MB VRAM (16bpp)
    private byte transferMode = 0;
    private int transferX, transferY, transferWidth, transferHeight;
    private int transferCount = 0;

    private uint gp1Control = 0;
    private Action<int, bool>? RaiseIRQ; // delegar IRQ para CPU

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
    }

    // <summary>
    /// Prepara o GPU para desenhar, limpando buffers e estados.
    /// /// Deve ser chamado antes de iniciar uma nova sequência de comandos.
    /// </summary>
    public void PrepareForDrawing()
    {
        Console.WriteLine("[GPU] Preparando GPU para desenho...");
        Reset();
    }

    public void WriteGP0(uint value)
    {
        byte cmd = (byte)(value >> 24);

        // Etapa 1: Preparar a transferência A0h
        if (transferMode == 1 && commandBuffer.Count < 3)
        {
            commandBuffer.Enqueue(value);

            if (commandBuffer.Count == 3)
            {
                ExecuteCommand(); // Executa SetupVRAMTransfer
                commandBuffer.Clear();
                transferMode = 2; // Agora começa a aceitar os dados da VRAM
            }
            return;
        }

        // Etapa 2: Receber dados da VRAM
        if (transferMode == 2)
        {
            UploadToVRAM(value);
            return;
        }

        // Etapa normal de comandos
        if (cmd == 0xA0)
        {
            commandBuffer.Clear();
            commandBuffer.Enqueue(value);
            transferMode = 1; // Começa a receber dados de setup
        }
        else
        {
            commandBuffer.Enqueue(value);
            if (IsCommandComplete())
            {
                ExecuteCommand();
                commandBuffer.Clear();
            }
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

            // 🟢 Gera IRQ0 se o bit 24 de gp1Control estiver habilitado
            if ((gp1Control & (1u << 24)) != 0)
            {
                Console.WriteLine("[GPU] IRQ0 gerado ao finalizar transferência");
                RaiseIRQ?.Invoke(0, true); // IRQ0
            }

            transferMode = 0;
            transferCount = 0;
        }
    }


    private bool IsCommandComplete()
    {
        byte cmd = (byte)(commandBuffer.Peek() >> 24);
        return cmd switch
        {
            0x20 => commandBuffer.Count >= 4, // Triângulo plano
            0x24 => commandBuffer.Count >= 7, // Triângulo texturizado → corrigido de 6 para 7
            0xA0 => commandBuffer.Count >= 3, // Transferência para VRAM (setup)
            _ => true,
        };
    }

    private void ExecuteCommand()
    {
        uint[] data = commandBuffer.ToArray();
        byte cmd = (byte)(data[0] >> 24);

        switch (cmd)
        {
            case 0x20:
                DrawFlatTriangle(data);
                break;
            case 0x24:
                DrawTexturedTriangle(data);
                break;
            case 0xA0:
                SetupVRAMTransfer(data);
                break;
            default:
                Console.WriteLine($"[GPU] Comando GP0 desconhecido: 0x{cmd:X2}");
                break;
        }
    }

    private void SetupVRAMTransfer(uint[] data)
    {
        // GP0 A0h: Copy Rectangle from CPU to VRAM
        transferX = (int)(data[1] & 0xFFFF);
        transferY = (int)(data[1] >> 16);
        transferWidth = (int)(data[2] & 0xFFFF);
        transferHeight = (int)(data[2] >> 16);
        transferCount = 0;
        Console.WriteLine($"[GPU] Transferência para VRAM: ({transferX},{transferY}) {transferWidth}x{transferHeight}");
    }

    private void DrawFlatTriangle(uint[] data)
    {
        uint color = data[0] & 0x00FFFFFF;

        Vertex2D v0 = ExtractVertex(data[1]);
        Vertex2D v1 = ExtractVertex(data[2]);
        Vertex2D v2 = ExtractVertex(data[3]);

        renderer.DrawFlatTriangle(v0, v1, v2, color);
    }

    private void DrawTexturedTriangle(uint[] data)
    {
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

