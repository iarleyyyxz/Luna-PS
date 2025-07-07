using Luna.Math; // Assumindo Vertex2D e VertexShaderVertex estão aqui
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using Vortice.Direct3D;
using System;
using System.IO;
using System.Runtime.InteropServices; // Para Marshal.SizeOf
using System.Collections.Generic; // Para Dictionary

using Luna.GPU; // Para ushort[,] (GetVramData)
using System.Numerics; // Para IGPURenderer, TexVertex

namespace Luna.Renderer.Direct3D11
{
    public class Direct3D11Renderer : IGPURenderer, IDisposable
    {
        private ID3D11Device device;
        private ID3D11DeviceContext context;
        private IDXGISwapChain swapChain;
        private ID3D11RenderTargetView renderTarget;

        // Shaders para quad fullscreen (VRAM)
        private D3D11Shader _vramShader;
        private D3D11VertexBuffer _vramQuadVB;
        private ID3D11Texture2D _vramTexture;
        private ID3D11ShaderResourceView _vramTextureSRV;
        private ID3D11SamplerState _vramSamplerState;
        private byte[] _vramRgb888 = new byte[1024 * 512 * 4]; // VRAM_WIDTH * VRAM_HEIGHT * 4 bytes/pixel

        // Shaders para desenho de triângulos (Flat e Textured)
        private D3D11Shader _flatTriangleShader;
        private D3D11Shader _texturedTriangleShader;
        private D3D11VertexBuffer _dynamicTriangleVB; // Para triângulos dinâmicos
        private ID3D11SamplerState _triangleSamplerState; // Pode ser o mesmo do VRAM

        // Gerenciamento de texturas para CreateTextureFromVRAM
        private Dictionary<int, ID3D11ShaderResourceView> _customTextures = new();
        private int _nextTextureId = 1;

        // Dimensões da janela e VRAM (constantes ou passadas)
        private const int VRAM_WIDTH = 1024;
        private const int VRAM_HEIGHT = 512;
        private int _windowWidth;
        private int _windowHeight;


        public Direct3D11Renderer(IntPtr windowHandle, int windowWidth, int windowHeight)
        {
            _windowWidth = windowWidth;
            _windowHeight = windowHeight;

            var desc = new SwapChainDescription
            {
                BufferCount = 1,
                BufferDescription = new ModeDescription((uint)windowWidth, (uint)windowHeight, new Rational(60, 1), Format.R8G8B8A8_UNorm),
                BufferUsage = Usage.RenderTargetOutput,
                OutputWindow = windowHandle,
                SampleDescription = new SampleDescription(1, 0),
                Windowed = true,
                SwapEffect = SwapEffect.Discard
            };
            FeatureLevel[] featureLevels = { FeatureLevel.Level_11_0 };
            FeatureLevel? featureLevel;
            D3D11.D3D11CreateDeviceAndSwapChain(
                null,
                DriverType.Hardware,
                DeviceCreationFlags.BgraSupport | DeviceCreationFlags.Debug, // Habilite o Debug Layer
                featureLevels,
                desc,
                out swapChain,
                out device,
                out featureLevel,
                out context
            );

            using (var backBuffer = swapChain.GetBuffer<ID3D11Texture2D>(0))
            {
                renderTarget = device.CreateRenderTargetView(backBuffer);
            }
            var viewport = new Viewport(0, 0, _windowWidth, _windowHeight, 0.0f, 1.0f);
            context.RSSetViewport(viewport);
            context.OMSetRenderTargets(renderTarget); // Definir o render target uma vez

            // --- Inicializar recursos para a VRAM (quad fullscreen) ---
            InitializeVramRendering();

            // --- Inicializar recursos para desenho de triângulos ---
            InitializeTriangleRendering();
        }

        private void InitializeVramRendering()
        {
            // Shaders HLSL para o quad fullscreen (VRAM)
            // Certifique-se de que estes arquivos .cso existem em Assets/Shaders
            string shaderDir = Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders");
            string vsPath = Path.Combine(shaderDir, "VramQuadVS.cso");
            string psPath = Path.Combine(shaderDir, "VramQuadPS.cso");

            if (!File.Exists(vsPath) || !File.Exists(psPath))
                throw new Exception($"[D3D11] Shaders da VRAM não encontrados!\nVS: {vsPath}\nPS: {psPath}");

            byte[] vsBytecode = File.ReadAllBytes(vsPath);
            byte[] psBytecode = File.ReadAllBytes(psPath);

            var inputElements = new[]
            {
                new InputElementDescription("POSITION", 0, Format.R32G32_Float, 0, 0),
                new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 8, 0)
            };
            _vramShader = new D3D11Shader(device, vsBytecode, psBytecode, inputElements);

            // Vértices para um quad fullscreen
            float[] vertices = new float[]
            {
                -1.0f, -1.0f, 0.0f, 1.0f, // Bottom-left (PosXY, UV)
                 1.0f, -1.0f, 1.0f, 1.0f, // Bottom-right
                -1.0f,  1.0f, 0.0f, 0.0f, // Top-left
                 1.0f,  1.0f, 1.0f, 0.0f  // Top-right
            };
           // _vramQuadVB = new D3D11VertexBuffer(device, MemoryMarshal.AsBytes(vertices.AsSpan()).ToArray(), (uint)Marshal.SizeOf<Vector4>(), 4);

            // Textura VRAM
            var vramTextureDesc = new Texture2DDescription()
            {
                Width = VRAM_WIDTH,
                Height = VRAM_HEIGHT,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.R8G8B8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default, // Permite UpdateSubresource
                BindFlags = BindFlags.ShaderResource,
                CPUAccessFlags = CpuAccessFlags.None,
                MiscFlags = ResourceOptionFlags.None
            };
            _vramTexture = device.CreateTexture2D(vramTextureDesc);
            _vramTextureSRV = device.CreateShaderResourceView(_vramTexture);

            // Sampler para a textura VRAM
            var samplerDesc = new SamplerDescription()
            {
                Filter = Filter.MinMagMipPoint, // Point filtering para pixels PS1
                AddressU = TextureAddressMode.Clamp,
                AddressV = TextureAddressMode.Clamp,
                AddressW = TextureAddressMode.Clamp,
                ComparisonFunc = ComparisonFunction.Never
            };
            _vramSamplerState = device.CreateSamplerState(samplerDesc);
        }

        private void InitializeTriangleRendering()
        {
            string shaderDir = Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders");

            // Shaders para Flat Triangles (Vertex2D -> VertexShaderVertex)
            string flatVsPath = Path.Combine(shaderDir, "FlatTriangleVS.cso");
            string flatPsPath = Path.Combine(shaderDir, "FlatTrianglePS.cso");
            if (!File.Exists(flatVsPath) || !File.Exists(flatPsPath))
                throw new Exception($"[D3D11] Shaders Flat Triangle não encontrados!\nVS: {flatVsPath}\nPS: {flatPsPath}");

            byte[] flatVsBytecode = File.ReadAllBytes(flatVsPath);
            byte[] flatPsBytecode = File.ReadAllBytes(flatPsPath);

            var flatInputElements = new[]
            {
                new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
                new InputElementDescription("COLOR", 0, Format.R32G32B32A32_Float, 12, 0) // Offset 12 bytes (3 floats * 4 bytes/float)
            };
            _flatTriangleShader = new D3D11Shader(device, flatVsBytecode, flatPsBytecode, flatInputElements);


            // Shaders para Textured Triangles (TexVertex)
            string texturedVsPath = Path.Combine(shaderDir, "TexturedTriangleVS.cso"); // Pode ser o mesmo do FlatTriangleVS
            string texturedPsPath = Path.Combine(shaderDir, "TexturedTrianglePS.cso");
            if (!File.Exists(texturedVsPath) || !File.Exists(texturedPsPath))
                throw new Exception($"[D3D11] Shaders Textured Triangle não encontrados!\nVS: {texturedVsPath}\nPS: {texturedPsPath}");

            byte[] texturedVsBytecode = File.ReadAllBytes(texturedVsPath);
            byte[] texturedPsBytecode = File.ReadAllBytes(texturedPsPath);

            var texturedInputElements = new[]
            {
                new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
                new InputElementDescription("COLOR", 0, Format.R32G32B32A32_Float, 12, 0),
                new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 28, 0) // Offset 12 (Pos) + 16 (Color) = 28
            };
            _texturedTriangleShader = new D3D11Shader(device, texturedVsBytecode, texturedPsBytecode, texturedInputElements);

            // Buffer de vértice dinâmico para triângulos (tamanho suficiente para vários triângulos)
            // Usamos TexVertex.SizeOf para garantir que é grande o suficiente para ambos os tipos de vértice.
            _dynamicTriangleVB = new D3D11VertexBuffer(device, Marshal.SizeOf<TexVertex>() * 64, (uint)Marshal.SizeOf<TexVertex>(), ResourceUsage.Dynamic, CpuAccessFlags.Write);

            // Sampler para texturas de triângulos (pode ser o mesmo da VRAM ou diferente)
            _triangleSamplerState = _vramSamplerState; // Reutilizando
        }

        // Este método não é necessário se você inicializar com o construtor
        public void Initialize(ushort[,] ushorts)
        {
            // Este método não é mais necessário com o novo construtor que aceita windowHandle
            // e o InitializeVramRendering/InitializeTriangleRendering.
            // Se precisar inicializar algo com a VRAM inicial, faça isso no construtor
            // ou em um método separado que você chame UMA VEZ após a construção.
            throw new NotSupportedException("O método Initialize(ushort[,]) não é mais suportado. Use o construtor com windowHandle.");
        }

        public void Clear()
        {
            context.ClearRenderTargetView(renderTarget, new Color4(0.1f, 0.1f, 0.6f, 1));
        }

        public void Present()
        {
            // Primeiro, desenhe a VRAM como um quad fullscreen
            context.OMSetRenderTargets(renderTarget); // Definir render target
            context.IASetInputLayout(_vramShader.InputLayout);
            context.VSSetShader(_vramShader.VertexShader);
            context.PSSetShader(_vramShader.PixelShader);
            context.IASetVertexBuffers(0, new[] { _vramQuadVB.Buffer }, new uint[] { _vramQuadVB.Stride }, new uint[] { 0 });
            context.IASetPrimitiveTopology(PrimitiveTopology.TriangleStrip);
            context.PSSetShaderResource(0, _vramTextureSRV);
            context.PSSetSampler(0, _vramSamplerState);
            context.Draw((uint)_vramQuadVB.VertexCount, 0);

            // Depois de desenhar a VRAM, você pode desenhar os triângulos por cima
            // Os DrawFlatTriangle e DrawTexturedTriangle devem chamar OMSetRenderTargets
            // e configurar os shaders/layouts apropriados.

            swapChain.Present(1, PresentFlags.None);
        }

        public void DrawFlatTriangle(Vertex2D v0, Vertex2D v1, Vertex2D v2, uint color)
        {
            var vertices = new[]
            {
                new VertexShaderVertex(v0.X, v0.Y, 0.5f, color),
                new VertexShaderVertex(v1.X, v1.Y, 0.5f, color),
                new VertexShaderVertex(v2.X, v2.Y, 0.5f, color)
            };

            var mappedResource = context.Map(_dynamicTriangleVB.Buffer, MapMode.WriteDiscard);

            // CORREÇÃO: Usar o tipo VertexShaderVertex aqui.
            // O segundo argumento de Marshal.Copy é o offset no array de origem (byte[]),
            // o terceiro é o ponteiro de destino (IntPtr),
            // o quarto é o número de bytes a serem copiados.
            System.Runtime.InteropServices.Marshal.Copy(
                System.Runtime.InteropServices.MemoryMarshal.AsBytes(vertices.AsSpan()).ToArray(),
                0,
                mappedResource.DataPointer,
                (int)(vertices.Length * Marshal.SizeOf<VertexShaderVertex>())
            );

            context.Unmap(_dynamicTriangleVB.Buffer, 0);

            context.IASetInputLayout(_flatTriangleShader.InputLayout);
            context.VSSetShader(_flatTriangleShader.VertexShader);
            context.PSSetShader(_flatTriangleShader.PixelShader);
            // CORREÇÃO: Stride deve ser o tamanho do VertexShaderVertex
            context.IASetVertexBuffers(0, new[] { _dynamicTriangleVB.Buffer }, new uint[] { (uint)Marshal.SizeOf<VertexShaderVertex>() }, new uint[] { 0 });
            context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);

            context.Draw(3, 0);
        }

        public void DrawTexturedTriangle(TexVertex v0, TexVertex v1, TexVertex v2, int textureId)
        {
            if (!_customTextures.TryGetValue(textureId, out var customTextureSRV))
            {
                Console.WriteLine($"[Renderer] ERRO: Textura com ID {textureId} não encontrada para desenhar triângulo texturizado.");
                return;
            }

            var vertices = new[] { v0, v1, v2 };

            var mappedResource = context.Map(_dynamicTriangleVB.Buffer, MapMode.WriteDiscard);

            // CORREÇÃO: Usar o tipo TexVertex aqui.
            System.Runtime.InteropServices.Marshal.Copy(
                System.Runtime.InteropServices.MemoryMarshal.AsBytes(vertices.AsSpan()).ToArray(),
                0,
                mappedResource.DataPointer,
                (int)(vertices.Length * Marshal.SizeOf<TexVertex>()) // <<-- CORREÇÃO: Usar Marshal.SizeOf<TexVertex>()
            );

            context.Unmap(_dynamicTriangleVB.Buffer, 0);

            context.IASetInputLayout(_texturedTriangleShader.InputLayout);
            context.VSSetShader(_texturedTriangleShader.VertexShader);
            context.PSSetShader(_texturedTriangleShader.PixelShader);
            // CORREÇÃO: Stride deve ser o tamanho do TexVertex
            context.IASetVertexBuffers(0, new[] { _dynamicTriangleVB.Buffer }, new uint[] { (uint)Marshal.SizeOf<TexVertex>() }, new uint[] { 0 }); // <<-- CORREÇÃO: Stride correto
            context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);

            context.PSSetShaderResource(0, customTextureSRV);
            context.PSSetSampler(0, _triangleSamplerState);

            context.Draw(3, 0);

            // Desligar o ShaderResourceView após o desenho para evitar referências persistentes desnecessárias
            context.PSSetShaderResource(0, null);
        }

        public int CreateTextureFromVRAM(ushort[,] vram, int x, int y, int width, int height)
        {
            // Validação básica para evitar texturas inválidas
            if (width <= 0 || height <= 0 || x < 0 || y < 0 || x >= VRAM_WIDTH || y >= VRAM_HEIGHT)
            {
                Console.WriteLine($"[Renderer] ERRO: Parâmetros inválidos para CreateTextureFromVRAM: x={x}, y={y}, width={width}, height={height}");
                return -1; // Retorna um ID inválido
            }

            // Garante que a área da textura não exceda os limites da VRAM
            int actualWidth = System.Math.Min(width, VRAM_WIDTH - x);
            int actualHeight = System.Math.Min(height, VRAM_HEIGHT - y);

            if (actualWidth <= 0 || actualHeight <= 0)
            {
                Console.WriteLine($"[Renderer] ERRO: Área de textura fora dos limites da VRAM: x={x}, y={y}, width={width}, height={height}");
                return -1;
            }

            byte[] textureData = new byte[actualWidth * actualHeight * 4]; // RGBA8888

            for (int h = 0; h < actualHeight; h++)
            {
                for (int w = 0; w < actualWidth; w++)
                {
                    // Obtém o pixel RGB555 da VRAM
                    ushort pix = vram[x + w, y + h];

                    // Converte RGB555 para RGBA8888
                    // RGB555: R (bits 0-4), G (bits 5-9), B (bits 10-14), Alpha (bit 15, geralmente 1 para opaco)
                    // PS1 Graphics Synthesizer geralmente não usa o bit 15 para transparência direta no RGB555.
                    // Para simplicidade, vamos usar 255 (opaco) para o canal alfa.
                    byte r = (byte)(((pix >> 0) & 0x1F) << 3);  // 5 bits -> 8 bits (x8)
                    byte g = (byte)(((pix >> 5) & 0x1F) << 3);  // 5 bits -> 8 bits (x8)
                    byte b = (byte)(((pix >> 10) & 0x1F) << 3); // 5 bits -> 8 bits (x8)

                    // Calcula o índice no array de bytes (RGBA de 4 bytes por pixel)
                    int idx = (h * actualWidth + w) * 4;
                    textureData[idx + 0] = r;
                    textureData[idx + 1] = g;
                    textureData[idx + 2] = b;
                    textureData[idx + 3] = 255; // Alpha sempre opaco por enquanto
                }
            }

            // Descrição da textura Direct3D11
            var textureDesc = new Texture2DDescription()
            {
                Width = (uint)actualWidth,
                Height = (uint)actualHeight,
                MipLevels = 1, // Sem mipmaps para simplicidade
                ArraySize = 1,
                Format = Format.R8G8B8A8_UNorm, // Formato correto para RGBA de 8 bits
                SampleDescription = new SampleDescription(1, 0), // Sem multi-sampling
                Usage = ResourceUsage.Immutable, // Dados são fornecidos na criação e não serão alterados
                BindFlags = BindFlags.ShaderResource, // Pode ser ligada como recurso de shader
                CPUAccessFlags = CpuAccessFlags.None, // CPU não precisa acessar a textura depois de criada
                MiscFlags = ResourceOptionFlags.None
            };

            // Fixa o array de bytes na memória para obter um ponteiro estável
            GCHandle gcHandle = GCHandle.Alloc(textureData, GCHandleType.Pinned);
            IntPtr textureDataPtr = gcHandle.AddrOfPinnedObject(); // Obtém o IntPtr para os dados

            ID3D11Texture2D newTexture = null;
            ID3D11ShaderResourceView newTextureSRV = null;

            try
            {
                // Cria os dados iniciais para a textura
                // O RowPitch é o tamanho em bytes de uma linha da textura.
                // Para RGBA8888, cada pixel tem 4 bytes.
                var initialData = new SubresourceData(textureDataPtr, (uint)actualWidth * 4); //
                newTexture = device.CreateTexture2D(textureDesc, initialData);
                newTextureSRV = device.CreateShaderResourceView(newTexture);

                // Armazena o SRV e retorna um ID único
                int id = _nextTextureId++;
                _customTextures.Add(id, newTextureSRV);

                Console.WriteLine($"[Renderer] Textura personalizada criada com ID {id} de VRAM({x},{y}) {actualWidth}x{actualHeight}");
                return id;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Renderer] ERRO ao criar textura de VRAM: {ex.Message}");
                // Libera os recursos mesmo em caso de erro
                newTextureSRV?.Dispose();
                newTexture?.Dispose();
                return -1;
            }
            finally
            {
                // É CRÍTICO liberar o GCHandle para "desfixar" o array
                // e permitir que o coletor de lixo o gerencie.
                if (gcHandle.IsAllocated)
                {
                    gcHandle.Free(); //
                }
            }
        }

        public void UpdateVRAMTexture(ushort[,] vram)
        {
            for (int y = 0; y < VRAM_HEIGHT; y++)
            {
                for (int x = 0; x < VRAM_WIDTH; x++)
                {
                    ushort pix = vram[x, y];
                    byte r = (byte)(((pix >> 0) & 0x1F) << 3);
                    byte g = (byte)(((pix >> 5) & 0x1F) << 3);
                    byte b = (byte)(((pix >> 10) & 0x1F) << 3);

                    int idx = (y * VRAM_WIDTH + x) * 4;
                    _vramRgb888[idx + 0] = r;
                    _vramRgb888[idx + 1] = g;
                    _vramRgb888[idx + 2] = b;
                    _vramRgb888[idx + 3] = 255;
                }
            }
            context.UpdateSubresource(_vramRgb888, _vramTexture, 0, VRAM_WIDTH * 4);
        }

        public void Dispose()
        {
            // Libere os recursos na ordem inversa de criação
            _triangleSamplerState?.Dispose();
            _vramSamplerState?.Dispose();
            _vramTextureSRV?.Dispose();
            _vramTexture?.Dispose();
            _vramQuadVB?.Dispose();
            _vramShader?.Dispose();

            _flatTriangleShader?.Dispose();
            _texturedTriangleShader?.Dispose();
            _dynamicTriangleVB?.Dispose();

            foreach (var srv in _customTextures.Values)
            {
                srv?.Dispose();
            }
            _customTextures.Clear();

            renderTarget?.Dispose();
            swapChain?.Dispose();
            context?.Dispose();
            device?.Dispose();
        }
    }
}