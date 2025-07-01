using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using Luna.Math;

public class OpenGLRenderer : IGPURenderer
{
    private int shaderProgram;
    private int vao, vbo;
    private Dictionary<int, int> textureCache = new();

    public OpenGLRenderer()
    {
        Initialize();
    }

    public void Initialize()
    {
      //  vertexShaderSource = File.ReadAllText("Luna/Assets/Shaders/vertex.glsl");

        shaderProgram = CompileShaders(vertexShaderSource, fragmentShaderSource);

        vao = GL.GenVertexArray();
        vbo = GL.GenBuffer();

        GL.BindVertexArray(vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, 1024, IntPtr.Zero, BufferUsageHint.DynamicDraw);

        int stride = 4 * sizeof(float);

        // Position
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0);
        GL.EnableVertexAttribArray(0);

        // UV
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 2 * sizeof(float));
        GL.EnableVertexAttribArray(1);

        GL.BindVertexArray(0);
    }

    public void Clear()
    {
        GL.ClearColor(Color4.Black);
        GL.Clear(ClearBufferMask.ColorBufferBit);
    }

    public void Present()
    {
        // Nada aqui, depende do backend (GLControl, GameWindow, etc.)
    }

    public void DrawFlatTriangle(Vertex2D v0, Vertex2D v1, Vertex2D v2, uint color)
    {
        // Para simplificar, ignorando flat triangle (foco é textura)
    }

    public void DrawTexturedTriangle(TexVertex v0, TexVertex v1, TexVertex v2, int textureId)
    {
        float[] vertices = {
            v0.X, v0.Y, v0.U, v0.V,
            v1.X, v1.Y, v1.U, v1.V,
            v2.X, v2.Y, v2.U, v2.V
        };

        GL.UseProgram(shaderProgram);
        GL.BindVertexArray(vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, vertices.Length * sizeof(float), vertices);

        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, textureId);
        GL.Uniform1(GL.GetUniformLocation(shaderProgram, "uTexture"), 0);

        GL.DrawArrays(PrimitiveType.Triangles, 0, 3);

        GL.BindVertexArray(0);
    }

    public int CreateTextureFromVRAM(ushort[,] vram, int x, int y, int width, int height)
    {
        byte[] rgbaData = new byte[width * height * 4];
        int idx = 0;

        for (int j = 0; j < height; j++)
        {
            for (int i = 0; i < width; i++)
            {
                ushort pixel = vram[x + i, y + j];

                byte r = (byte)((pixel & 0x1F) << 3);
                byte g = (byte)(((pixel >> 5) & 0x1F) << 3);
                byte b = (byte)(((pixel >> 10) & 0x1F) << 3);

                rgbaData[idx++] = r;
                rgbaData[idx++] = g;
                rgbaData[idx++] = b;
                rgbaData[idx++] = 255;
            }
        }

        int texId = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, texId);

        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
            width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, rgbaData);

        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        return texId;
    }

    private int CompileShaders(string vertexSource, string fragmentSource)
    {
        int vertexShader = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vertexShader, vertexSource);
        GL.CompileShader(vertexShader);
        CheckShader(vertexShader);

        int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(fragmentShader, fragmentSource);
        GL.CompileShader(fragmentShader);
        CheckShader(fragmentShader);

        int program = GL.CreateProgram();
        GL.AttachShader(program, vertexShader);
        GL.AttachShader(program, fragmentShader);
        GL.LinkProgram(program);
        GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int status);
        if (status == 0)
            throw new Exception(GL.GetProgramInfoLog(program));

        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);

        return program;
    }

    private void CheckShader(int shader)
    {
        GL.GetShader(shader, ShaderParameter.CompileStatus, out int status);
        if (status == 0)
            throw new Exception(GL.GetShaderInfoLog(shader));
    }

    private const string vertexShaderSource = @"
#version 330 core
layout(location = 0) in vec2 aPos;
layout(location = 1) in vec2 aUV;
out vec2 TexCoord;
void main()
{
    gl_Position = vec4((aPos / vec2(320, 240)) * 2.0 - 1.0, 0.0, 1.0); // PS1: 320x240
    TexCoord = aUV;
}";

    private const string fragmentShaderSource = @"
#version 330 core
in vec2 TexCoord;
out vec4 FragColor;
uniform sampler2D uTexture;
void main()
{
    FragColor = texture(uTexture, TexCoord);
}";
}
