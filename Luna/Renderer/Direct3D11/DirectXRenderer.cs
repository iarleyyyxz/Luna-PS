using System;
using Luna.Math;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

public class DirectXRenderer : IGPURenderer
{
    private ID3D11Device device;
    private ID3D11DeviceContext context;
    private IDXGISwapChain swapChain;
    private ID3D11RenderTargetView renderTarget;

    public DirectXRenderer(IntPtr windowHandle)
    {
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

        D3D11.D3D11CreateDeviceAndSwapChain(null, Driver.Hardware, DeviceCreationFlags.None,
            null, desc, out device, out context, out swapChain);

        var backBuffer = swapChain.GetBuffer<ID3D11Texture2D>(0);
        renderTarget = device.CreateRenderTargetView(backBuffer);
    }

    public void Clear()
    {
        context.ClearRenderTargetView(renderTarget, new Color4(0, 0, 0, 1));
    }

    public void Present()
    {
        swapChain.Present(1, PresentFlags.None);
    }

    public int CreateTextureFromVRAM(ushort[,] vram, int x, int y, int width, int height)
    {
        throw new NotImplementedException("Criação de textura ainda não implementada.");
    }

    public void DrawFlatTriangle(Vertex2D v0, Vertex2D v1, Vertex2D v2, uint color)
    {
        throw new NotImplementedException("Desenho de triângulo não implementado.");
    }

    public void DrawTexturedTriangle(TexVertex v0, TexVertex v1, TexVertex v2, int textureId)
    {
        throw new NotImplementedException("Desenho de triângulo com textura ainda não implementado.");
    }

}
