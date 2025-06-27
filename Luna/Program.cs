using OpenTK.Windowing.Desktop;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL;
using Renderer.OpenGL;

class Program
{
    static void Main()
    {
        var nativeSettings = new NativeWindowSettings()
        {
            Size = new Vector2i(800, 600),
            Title = "OpenGLRenderer Demo"
        };

        using var window = new GameWindow(GameWindowSettings.Default, nativeSettings);
        var renderer = new OpenGLRenderer();

        window.Load += () =>
        {
            GL.Viewport(0, 0, window.Size.X, window.Size.Y);
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();
            // Define ortho 2D para coordenadas simples (0,0) canto inferior esquerdo
            GL.Ortho(0, window.Size.X, 0, window.Size.Y, -1, 1);
            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();
        };

        window.RenderFrame += (FrameEventArgs e) =>
        {
            renderer.Clear();

            // Exemplo: desenha um triângulo no meio da tela
            renderer.RenderPolyF3(
                new Vector2(400, 300),
                new Vector2(500, 300),
                new Vector2(450, 400),
                255, 100, 100);

            window.SwapBuffers();
        };

        window.Run();
    }
}
