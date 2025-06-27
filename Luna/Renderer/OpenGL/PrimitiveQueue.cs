using Renderer.OpenGL;

namespace Renderer.OpenGL
{
    public class PrimitiveQueue
    {
        private readonly List<DrawCommand> _commands = new();

        public void Add(DrawCommand command) => _commands.Add(command);

        public void Clear() => _commands.Clear();

        public void RenderAll()
        {
            foreach (var cmd in _commands)
                cmd.Render();
        }
    }
}

