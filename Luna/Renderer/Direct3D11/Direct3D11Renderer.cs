
using Luna.Math;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using Vortice.Direct3D;
using System;
using System.IO;
using Luna.Renderer.Direct3D11;
using Luna.GPU;

public class Direct3D11Renderer : IGPURenderer, IDisposable
{
    private ID3D11Device device;
    private ID3D11DeviceContext context;
    private IDXGISwapChain swapChain;
    private ID3D11RenderTargetView renderTarget;
    private D3D11Pipeline pipeline;
    private D3D11Shader shader;
    private D3D11VertexBuffer vertexBuffer;

    public Direct3D11Renderer(IntPtr windowHandle)
    {
        var desc = new SwapChainDescription
        {
            BufferCount = 1,
            BufferDescription = new ModeDescription(640, 480, new Rational(60, 1), Format.R8G8B8A8_UNorm),
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
            DeviceCreationFlags.BgraSupport,
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
        var viewport = new Viewport(0, 0, 640, 480, 0.0f, 1.0f);
        context.RSSetViewport(viewport);

        // Carrega shader mínimo (flat color)
        string shaderDir = Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders");
        string vsPath = Path.Combine(shaderDir, "SimpleVertexShader.cso");
        string psPath = Path.Combine(shaderDir, "SimplePixelShader.cso");
        if (!File.Exists(vsPath) || !File.Exists(psPath))
            throw new Exception($"[D3D11] Shader bytecode não encontrado!\nVS: {vsPath}\nPS: {psPath}");
        byte[] vsBytecode = File.ReadAllBytes(vsPath);
        byte[] psBytecode = File.ReadAllBytes(psPath);
        var inputElements = new[]
        {
            new InputElementDescription("POSITION", 0, Vortice.DXGI.Format.R32G32B32_Float, 0, 0),
            new InputElementDescription("COLOR", 0, Vortice.DXGI.Format.R32G32B32A32_Float, 12, 0)
        };
        shader = new D3D11Shader(device, vsBytecode, psBytecode, inputElements);
        pipeline = new D3D11Pipeline(shader);
    }

    public void Clear()
    {
        context.ClearRenderTargetView(renderTarget, new Color4(0.1f, 0.1f, 0.6f, 1));
    }

    public void Present()
    {
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
        uint stride = (uint)System.Runtime.InteropServices.Marshal.SizeOf<VertexShaderVertex>();
        var vertexData = System.Runtime.InteropServices.MemoryMarshal.AsBytes(vertices.AsSpan());
        vertexBuffer?.Dispose();
        vertexBuffer = new D3D11VertexBuffer(device, vertexData.ToArray(), (int)(stride * 3));
        context.OMSetRenderTargets(renderTarget);
        context.IASetInputLayout(shader.InputLayout);
        context.VSSetShader(shader.VertexShader);
        context.PSSetShader(shader.PixelShader);
        context.IASetVertexBuffers(0, new[] { vertexBuffer.Buffer }, new uint[] { stride }, new uint[] { 0 });
        context.IASetPrimitiveTopology(Vortice.Direct3D.PrimitiveTopology.TriangleList);
        context.Draw(3, 0);
    }

    public void DrawTexturedTriangle(TexVertex v0, TexVertex v1, TexVertex v2, int textureId)
    {
        // Não implementado neste exemplo mínimo
    }

    public int CreateTextureFromVRAM(ushort[,] vram, int x, int y, int width, int height)
    {
        // Não implementado neste exemplo mínimo
        return 0;
    }

    public void Dispose()
    {
        vertexBuffer?.Dispose();
        pipeline?.Dispose();
        renderTarget?.Dispose();
        swapChain?.Dispose();
        context?.Dispose();
        device?.Dispose();
    }

    public void UpdateVRAMTexture(ushort[,] vram)
    {
        throw new NotImplementedException();
    }
}
