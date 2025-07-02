using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using Luna.Math;

public class OpenGLRenderer : IGPURenderer
{
    private int vramTextureId;
    private int shaderProgram;
    private int vao, vbo;

    public OpenGLRenderer()
    {
        InitGL();
    }

    private void InitGL()
    {
        // Cria textura VRAM 1024x512 (16 bits RGB555)
        vramTextureId = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, vramTextureId);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgb5A1, 1024, 512, 0,
                      PixelFormat.Bgra, PixelType.UnsignedShort5551, IntPtr.Zero);

        // Configura filtro nearest (sem blur)
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

        // Configura wrap (clamp)
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        GL.BindTexture(TextureTarget.Texture2D, 0);

        // Inicializa shader, VAO, VBO etc. (vou mostrar no próximo passo)
        SetupShader();
        SetupBuffers();
    }

    // Atualiza textura VRAM (chame toda vez que VRAM for modificada)
    public void UpdateVRAMTexture(ushort[,] vram)
    {
        GL.BindTexture(TextureTarget.Texture2D, vramTextureId);

        // PS1 VRAM é RGB555, OpenGL aceita UnsignedShort5551
        // Mas seu array é ushort[,] (1024x512), converta para IntPtr

        unsafe
        {
            fixed (ushort* ptr = &vram[0, 0])
            {
                // Atualiza toda a textura
                GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, 1024, 512,
                                 PixelFormat.Bgra, PixelType.UnsignedShort5551, (IntPtr)ptr);
            }
        }
        GL.BindTexture(TextureTarget.Texture2D, 0);
    }

    private void SetupShader()
    {
        string vertexShaderSource = @"
    #version 330 core
    layout(location = 0) in vec2 aPos;
    layout(location = 1) in vec2 aTexCoord;

    out vec2 TexCoord;

    void main()
    {
        float x = (aPos.x / (1024.0 / 2.0)) - 1.0;
        float y = 1.0 - (aPos.y / (512.0 / 2.0));
        gl_Position = vec4(x, y, 0.0, 1.0);
        TexCoord = aTexCoord;
    }";

        string fragmentShaderSource = @"
    #version 330 core
    in vec2 TexCoord;
    out vec4 FragColor;

    uniform sampler2D vramTexture;

    void main()
    {
        FragColor = texture(vramTexture, TexCoord);
    }";

        int vertexShader = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vertexShader, vertexShaderSource);
        GL.CompileShader(vertexShader);
        // verificar compilação...

        int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(fragmentShader, fragmentShaderSource);
        GL.CompileShader(fragmentShader);
        // verificar compilação...

        shaderProgram = GL.CreateProgram();
        GL.AttachShader(shaderProgram, vertexShader);
        GL.AttachShader(shaderProgram, fragmentShader);
        GL.LinkProgram(shaderProgram);
        // verificar linkagem...

        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);
    }

    private void SetupBuffers()
    {
        vao = GL.GenVertexArray();
        vbo = GL.GenBuffer();

        GL.BindVertexArray(vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);

        // 4 floats por vértice: x,y,u,v
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);

        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));

        GL.BindVertexArray(0);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
    }

    public void Clear()
    {

    }

    public void DrawFlatTriangle(Vertex2D v0, Vertex2D v1, Vertex2D v2, uint color)
    {
        throw new NotImplementedException();
    }

    public void DrawTexturedTriangle(TexVertex v0, TexVertex v1, TexVertex v2, int textureId)
    {
        // Monta array intercalado: [x,y,u,v]
        float[] vertices = new float[]
        {
        v0.X, v0.Y, v0.U, v0.V,
        v1.X, v1.Y, v1.U, v1.V,
        v2.X, v2.Y, v2.U, v2.V,
        };

        GL.BindTexture(TextureTarget.Texture2D, vramTextureId);

        GL.UseProgram(shaderProgram);

        GL.BindVertexArray(vao);

        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.DynamicDraw);

        GL.DrawArrays(PrimitiveType.Triangles, 0, 3);

        GL.BindVertexArray(0);
        GL.UseProgram(0);
        GL.BindTexture(TextureTarget.Texture2D, 0);
    }


    public int CreateTextureFromVRAM(ushort[,] vram, int x, int y, int width, int height)
    {
        int textureId = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, textureId);

        // Copia os dados da VRAM 16bpp para um array de ushort (linha contínua)
        ushort[] buffer = new ushort[width * height];

        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                buffer[row * width + col] = vram[x + col, y + row];
            }
        }

        // Unsafe pointer fix para enviar diretamente ao OpenGL
        unsafe
        {
            fixed (ushort* ptr = &buffer[0])
            {
                GL.TexImage2D(TextureTarget.Texture2D, 0,
                    PixelInternalFormat.Rgb5A1, // OpenGL formato interno (ex: RGB555 ou similar)
                    width, height, 0,
                    PixelFormat.Bgra, PixelType.UnsignedShort5551,
                    (IntPtr)ptr);
            }
        }

        // Configurações de textura
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

        return textureId;
    }



    public void Present()
    {
        // Limpa o frame (opcional, dependendo de como você organiza a renderização)
      //  GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        // Aqui você pode renderizar todos os triângulos/texturas acumulados (se tiver algum batching)
        // ou simplesmente deixar vazio se você já desenhou tudo antes deste ponto

        // Se você usa double buffering (com GameWindow), você não precisa chamar SwapBuffers aqui
        // pois o OpenTK faz isso automaticamente após OnRenderFrame()
    }


    // ... resto da classe no próximo passo
}
