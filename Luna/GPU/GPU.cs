// Estrutura simplificada de um pacote POLY_F3
// Geralmente o pacote do PS1 vem em 4 palavras de 32 bits (4 uints)
using Renderer.OpenGL;
using OpenTK.Mathematics;
public struct GPUCommand
{
    public uint Cmd;  // Comando POLY_F3: 0x20 no byte mais baixo
    public uint Vertex1;
    public uint Vertex2;
    public uint Vertex3;
}

public class GPU
{
    OpenGLRenderer renderer;

    public GPU(OpenGLRenderer renderer)
    {
        this.renderer = renderer;
    }

    public void ProcessCommand(GPUCommand cmd)
    {
        byte commandType = (byte)(cmd.Cmd & 0xFF);
        if (commandType == 0x20) // POLY_F3
        {
            // Extrair cor flat do pacote (normalmente nos bits altos do Cmd)
            byte r = (byte)((cmd.Cmd >> 16) & 0xFF);
            byte g = (byte)((cmd.Cmd >> 8) & 0xFF);
            byte b = (byte)(cmd.Cmd & 0xFF);

            // Extrair as coordenadas dos vértices (cada 16 bits = x,y)
            float x1 = (short)(cmd.Vertex1 & 0xFFFF);
            float y1 = (short)((cmd.Vertex1 >> 16) & 0xFFFF);

            float x2 = (short)(cmd.Vertex2 & 0xFFFF);
            float y2 = (short)((cmd.Vertex2 >> 16) & 0xFFFF);

            float x3 = (short)(cmd.Vertex3 & 0xFFFF);
            float y3 = (short)((cmd.Vertex3 >> 16) & 0xFFFF);

            // Chama o renderizador OpenGL
            renderer.RenderPolyF3(
                new Vector2(x1, y1),
                new Vector2(x2, y2),
                new Vector2(x3, y3),
                r, g, b);
        }
        else
        {
            Console.WriteLine($"Comando GPU desconhecido: 0x{commandType:X2}");
        }
    }
}
