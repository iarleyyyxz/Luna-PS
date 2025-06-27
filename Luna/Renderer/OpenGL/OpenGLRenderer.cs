using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Renderer.OpenGL
{
    public class OpenGLRenderer
    {
        public OpenGLRenderer()
        {
            // Aqui pode inicializar estados do OpenGL
            GL.ClearColor(0f, 0f, 0f, 1f);
        }

        public void RenderPolyF3(Vector2 v1, Vector2 v2, Vector2 v3, byte r, byte g, byte b)
        {
            // Normaliza cores para 0..1
            float red = r / 255f;
            float green = g / 255f;
            float blue = b / 255f;

            // Configura a cor do polígono
            GL.Color3(red, green, blue);

            GL.Begin(PrimitiveType.Triangles);

            GL.Vertex2(v1.x, v1.y);
            GL.Vertex2(v2.x, v2.y);
            GL.Vertex2(v3.x, v3.y);

            GL.End();
        }

        public void Clear()
        {
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        }

        public void Present()
        {
            // Aqui você deve chamar o SwapBuffers do seu contexto OpenGL,
            // que depende da sua janela/framework (ex: GameWindow no OpenTK).
        }
    }
}

