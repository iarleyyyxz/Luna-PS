using System;
using Luna.Math;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using Vortice.Direct3D;
using System.Runtime.CompilerServices;
using System.Numerics;
using Luna.Renderer.Direct3D11;

// Renderizador Direct3D11 usando Vortice.Direct3D11
public class Direct3D11Renderer : IGPURenderer, IDisposable
{
    private ID3D11Device device;
    private ID3D11DeviceContext context;
    private IDXGISwapChain swapChain;
    private ID3D11RenderTargetView renderTarget;

    // Shader e flag de carregamento
    private D3D11Shader simpleShader;
    private bool shaderLoaded = false;

    // Shader para textura
    private D3D11Shader textureShader;
    private bool textureShaderLoaded = false;
    // Dicionário para texturas
    private System.Collections.Generic.Dictionary<int, ID3D11ShaderResourceView> textures = new();

    public Direct3D11Renderer(IntPtr windowHandle)
    {
        // Inicializa o dispositivo Direct3D11, contexto e swapchain
        // Descrição da SwapChain
        var desc = new SwapChainDescription
        {
            BufferCount = 1,
            BufferDescription = new ModeDescription(800, 600, new Rational(60, 1), Format.R8G8B8A8_UNorm),
            BufferUsage = Usage.RenderTargetOutput,
            OutputWindow = windowHandle,
            SampleDescription = new SampleDescription(1, 0),
            Windowed = true,
            SwapEffect = SwapEffect.Discard
        };

        // Níveis de recurso desejados
        FeatureLevel[] featureLevels = { FeatureLevel.Level_11_0 };
        FeatureLevel? featureLevel;

        // Criação do dispositivo, contexto e swapchain
        D3D11.D3D11CreateDeviceAndSwapChain(
            null,
            DriverType.Hardware,
            DeviceCreationFlags.BgraSupport,
            featureLevels,
            desc,
            out swapChain,
            out device,
            out featureLevel,
            out context
        );

        // Criação da render target view
        using (var backBuffer = swapChain.GetBuffer<ID3D11Texture2D>(0))
        {
            renderTarget = device.CreateRenderTargetView(backBuffer);
        }
    }

    // Limpa a tela com cor preta
    public void Clear()
    {
        context.ClearRenderTargetView(renderTarget, new Color4(0, 0, 0, 1));
    }

    // Apresenta o frame na tela
    public void Present()
    {
        swapChain.Present(1, PresentFlags.None);
    }

    // Criação de textura a partir da VRAM (não implementado)
    public int CreateTextureFromVRAM(ushort[,] vram, int x, int y, int width, int height)
    {
        // Cria uma textura 32-bit RGBA a partir da VRAM (16-bit)
        // VRAM: cada ushort é um pixel em formato 5:5:5:1 (R5G5B5A1)
        uint texWidth = (uint)width;
        uint texHeight = (uint)height;
        int pixelCount = (int)(texWidth * texHeight);
        byte[] rgba = new byte[pixelCount * 4];

        for (int j = 0; j < texHeight; j++)
        {
            for (int i = 0; i < texWidth; i++)
            {
                ushort src = vram[y + j, x + i];
                // Extrai R, G, B, A do formato 5:5:5:1
                byte r = (byte)(((src >> 0) & 0x1F) << 3);
                byte g = (byte)(((src >> 5) & 0x1F) << 3);
                byte b = (byte)(((src >> 10) & 0x1F) << 3);
                byte a = (byte)(((src >> 15) & 0x01) * 255);
                int idx = (j * (int)texWidth + i) * 4;
                rgba[idx + 0] = r;
                rgba[idx + 1] = g;
                rgba[idx + 2] = b;
                rgba[idx + 3] = a;
            }
        }

        var texDesc = new Texture2DDescription()
        {
            Width = texWidth,
            Height = texHeight,
            MipLevels = 1,
            ArraySize = 1,
            Format = Vortice.DXGI.Format.R8G8B8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Default,
            BindFlags = BindFlags.ShaderResource,
            CPUAccessFlags = CpuAccessFlags.None
        };

        // Cria a textura usando ponteiro fixo
        unsafe
        {
            fixed (byte* pRgba = rgba)
            {
                var dataBox = new SubresourceData((nint)pRgba, texWidth * 4);
                var texture = device.CreateTexture2D(texDesc, new[] { dataBox });

                // Cria ShaderResourceView para uso em pixel shader
                var srvDesc = new ShaderResourceViewDescription(texture, ShaderResourceViewDimension.Texture2D);
                var srv = device.CreateShaderResourceView(texture, srvDesc);

                // Armazena a textura/srv em um dicionário para uso posterior
                int texId = srv.GetHashCode();
                textures[texId] = srv;
                return texId;
            }
        }
    }


    public void DrawFlatTriangle(Vertex2D v0, Vertex2D v1, Vertex2D v2, uint color)
    {
        if (!shaderLoaded)
        {
            // Carrega bytecode dos shaders compilados
            byte[] vsBytecode = System.IO.File.ReadAllBytes("Assets/Shaders/SimpleVertexShader.cso");
            byte[] psBytecode = System.IO.File.ReadAllBytes("Assets/Shaders/SimplePixelShader.cso");
            var inputElements = new[]
            {
                new InputElementDescription("POSITION", 0, Vortice.DXGI.Format.R32G32B32_Float, 0, 0),
                new InputElementDescription("COLOR", 0, Vortice.DXGI.Format.R32G32B32A32_Float, 12, 0)
            };
            simpleShader = new Luna.Renderer.Direct3D11.D3D11Shader(device, vsBytecode, psBytecode, inputElements);
            shaderLoaded = true;
        }

        // Estrutura de vértice compatível com o shader
        var vertices = new[]
        {
            new VertexShaderVertex(v0.X, v0.Y, 0, color),
            new VertexShaderVertex(v1.X, v1.Y, 0, color),
            new VertexShaderVertex(v2.X, v2.Y, 0, color)
        };

        // Cria o vertex buffer

        uint stride = (uint)System.Runtime.InteropServices.Marshal.SizeOf<VertexShaderVertex>();
        var vertexBufferDesc = new Vortice.Direct3D11.BufferDescription(
            stride * 3,
            BindFlags.VertexBuffer,
            ResourceUsage.Immutable
        );
        // Corrige: Cria buffer usando MemoryMarshal
        var vertexData = System.Runtime.InteropServices.MemoryMarshal.AsBytes(vertices.AsSpan());
        using var vertexBuffer = device.CreateBuffer(vertexData, vertexBufferDesc);

        // Configura pipeline
        context.IASetInputLayout(simpleShader.InputLayout);
        context.VSSetShader(simpleShader.VertexShader);
        context.PSSetShader(simpleShader.PixelShader);
        context.IASetVertexBuffers(0, new[] { vertexBuffer }, new uint[] { stride }, new uint[] { 0 });
        context.IASetPrimitiveTopology(Vortice.Direct3D.PrimitiveTopology.TriangleList);
        context.Draw(3, 0);
    }


    // Desenha um triângulo texturizado
    public void DrawTexturedTriangle(TexVertex v0, TexVertex v1, TexVertex v2, int textureId)
    {
        if (!textureShaderLoaded)
        {
            // Carrega bytecode dos shaders de textura compilados
            byte[] vsBytecode = System.IO.File.ReadAllBytes("Assets/Shaders/SimpleTexturedVertexShader.cso");
            byte[] psBytecode = System.IO.File.ReadAllBytes("Assets/Shaders/SimpleTexturedPixelShader.cso");
            var inputElements = new[]
            {
                new InputElementDescription("POSITION", 0, Vortice.DXGI.Format.R32G32B32_Float, 0, 0),
                new InputElementDescription("TEXCOORD", 0, Vortice.DXGI.Format.R32G32_Float, 12, 0)
            };
            textureShader = new D3D11Shader(device, vsBytecode, psBytecode, inputElements);
            textureShaderLoaded = true;
        }

        // Estrutura de vértice para textura
        var vertices = new[]
        {
            new TexturedVertex(v0.X, v0.Y, 0, v0.U, v0.V),
            new TexturedVertex(v1.X, v1.Y, 0, v1.U, v1.V),
            new TexturedVertex(v2.X, v2.Y, 0, v2.U, v2.V)
        };

        uint stride = (uint)System.Runtime.InteropServices.Marshal.SizeOf<TexturedVertex>();
        var vertexBufferDesc = new BufferDescription(
            stride * 3,
            BindFlags.VertexBuffer,
            ResourceUsage.Immutable
        );
        var vertexData = System.Runtime.InteropServices.MemoryMarshal.AsBytes(vertices.AsSpan());
        using var vertexBuffer = device.CreateBuffer(vertexData, vertexBufferDesc);

        // Recupera a textura pelo id
        if (!textures.TryGetValue(textureId, out var srv))
            return;

        // Configura pipeline
        context.IASetInputLayout(textureShader.InputLayout);
        context.VSSetShader(textureShader.VertexShader);
        context.PSSetShader(textureShader.PixelShader);
        context.IASetVertexBuffers(0, new[] { vertexBuffer }, new uint[] { stride }, new uint[] { 0 });
        context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        context.PSSetShaderResource(0, srv);
        context.Draw(3, 0);
    }

    // Estrutura de vértice para textura
    private struct TexturedVertex
    {
        public float X, Y, Z;
        public float U, V;
        public TexturedVertex(float x, float y, float z, float u, float v)
        {
            X = x; Y = y; Z = z; U = u; V = v;
        }
    }

    // Liberação dos recursos Direct3D
    public void Dispose()
    {
        renderTarget?.Dispose();
        swapChain?.Dispose();
        context?.Dispose();
        device?.Dispose();
    }
}
