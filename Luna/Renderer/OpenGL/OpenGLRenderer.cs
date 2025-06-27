using Renderer.OpenGL;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Renderer.OpenGL
{
    public class OpenGLRenderer
    {
        private PrimitiveQueue _queue = new();

        public void Clear()
        {
            GL.ClearColor(Color4.Brown);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            _queue.Clear();
        }

        public void QueuePolyF3(Vector2 v0, Vector2 v1, Vector2 v2, Color4 color)
        {
            _queue.Add(new PolyF3(v0, v1, v2, color));
        }

        public void RenderFrame()
        {
            GL.Disable(EnableCap.CullFace);
            
            _queue.RenderAll();  // ou renderer.RenderFrame();

            GL.Enable(EnableCap.CullFace); // reativa depois, se necessário


        }
    }
}
