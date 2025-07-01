using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;

public class EmuWindow : GameWindow
{
    private GPU gpu;
    private OpenGLRenderer renderer;

    public EmuWindow(GameWindowSettings gws, NativeWindowSettings nws) : base(gws, nws)
    {
        renderer = new OpenGLRenderer();
        gpu = new GPU(renderer);
    }

    protected override void OnLoad()
    {
        base.OnLoad();
        GL.Viewport(0, 0, Size.X, Size.Y);

        renderer = new OpenGLRenderer();
        gpu = new GPU(renderer);

        LoadDemoTextureAndDrawTriangle();
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);

       // GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        gpu.Render();

        SwapBuffers();
    }

    private void LoadDemoTextureAndDrawTriangle()
    {
        // Simula uma textura 16x16 preenchida com azul
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 16; x++)
            WriteToVRAM(x, y, RGB555(0, 0, 31)); // Azul

        // Simula comando GP0 A0h (Copy CPU→VRAM): carregar textura azul em (0,0)
        gpu.WriteGP0(0xA0000000); // cmd
        gpu.WriteGP0(0x00000000); // x=0, y=0
        gpu.WriteGP0(0x00100010); // w=16, h=16

        for (int i = 0; i < (16 * 16) / 2; i++) // 2 pixels por uint
        {
            gpu.WriteGP0(0x7C1F7C1F); // Azul repetido
        }

        // GP0 24h: triângulo texturizado
        gpu.WriteGP0(0x24000000); // CMD + cor

        // V0: pos (100,100), uv (0,0)
        gpu.WriteGP0((100 << 16) | 100);
        gpu.WriteGP0((0 << 8) | 0);

        // V1: pos (200,100), uv (15,0)
        gpu.WriteGP0((100 << 16) | 200);
        gpu.WriteGP0((0 << 8) | 15);

        // V2: pos (150,200), uv (7,15)
        gpu.WriteGP0((200 << 16) | 150);
        gpu.WriteGP0((15 << 8) | 7);
    }

    private void WriteToVRAM(int x, int y, ushort value)
    {
        // Simula carregamento manual (opcional)
    }

    private ushort RGB555(byte r, byte g, byte b)
    {
        return (ushort)((b << 10) | (g << 5) | r);
    }
}
