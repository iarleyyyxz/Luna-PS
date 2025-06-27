using OpenTK.Graphics.GL;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;
using Renderer.OpenGL;
public class PolyF3 : DrawCommand
{
    public Vector2[] Vertices { get; }
    public Color4 Color { get; }

    public PolyF3(Vector2 v0, Vector2 v1, Vector2 v2, Color4 color)
    {
        Vertices = new[] { v0, v1, v2 };
        Color = color;
    }

    public override void Render()
    {
        GL.Color4(Color);
        GL.Begin(PrimitiveType.Triangles);
        foreach (var v in Vertices)
            GL.Vertex2(v.X, v.Y);
        GL.End();
    }
}