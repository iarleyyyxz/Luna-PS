using Luna.Math;

public interface IGPURenderer
{
    void Clear();
    void DrawFlatTriangle(Vertex2D v0, Vertex2D v1, Vertex2D v2, uint color);
    void DrawTexturedTriangle(TexVertex v0, TexVertex v1, TexVertex v2, int textureId);
    int CreateTextureFromVRAM(ushort[,] vram, int x, int y, int width, int height);
    void Present();
}
