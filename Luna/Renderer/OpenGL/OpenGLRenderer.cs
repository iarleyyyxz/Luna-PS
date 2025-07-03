
using OpenTK.Graphics.OpenGL4;
using System;
using Luna.Math;
using Luna.Renderer;
using Luna.Renderer.OpenGL;
using Luna.GPU;

public class OpenGLRenderer : IGPURenderer, IDisposable
{
    private OpenGLPipeline pipeline;
    private OpenGLVertexBuffer vertexBuffer;
    private int vao;
    private float[] triangleVertices;

    public OpenGLRenderer()
    {
        // Shader mínimo: posição, cor fixa
        string vertexSrc = "#version 330 core\n"
            + "layout(location = 0) in vec2 inPos;\n"
            + "void main() { gl_Position = vec4(inPos, 0.0, 1.0); }\n";
        string fragmentSrc = "#version 330 core\n"
            + "out vec4 FragColor;\n"
            + "void main() { FragColor = vec4(1,0,0,1); }\n";
        var shader = new OpenGLShader(vertexSrc, fragmentSrc);
        pipeline = new OpenGLPipeline(shader);
        vertexBuffer = new OpenGLVertexBuffer();
        vao = GL.GenVertexArray();
        GL.BindVertexArray(vao);
        vertexBuffer.Bind();
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
        GL.BindVertexArray(0);
        vertexBuffer.Unbind();
    }


    // Framebuffer para VRAM PS1 (1024x512, 16bpp)
    private int vramTexture = 0;
    private bool vramDirty = true;
    private byte[] vramRgb = new byte[1024 * 512 * 3];

    public void UpdateVRAMTexture(ushort[,] vram)
    {
        // Converte VRAM 16bpp (RGB555, ignora bit 15) para RGB888
        for (int y = 0; y < 512; y++)
        for (int x = 0; x < 1024; x++)
        {
            ushort pix = vram[x, y];
            byte r = (byte)(((pix >> 0) & 0x1F) * 255 / 31);
            byte g = (byte)(((pix >> 5) & 0x1F) * 255 / 31);
            byte b = (byte)(((pix >> 10) & 0x1F) * 255 / 31);
            int idx = (y * 1024 + x) * 3;
            vramRgb[idx + 0] = r;
            vramRgb[idx + 1] = g;
            vramRgb[idx + 2] = b;
        }
        vramDirty = true;
    }
    private void EnsureVRAMTexture()
    {
        if (vramTexture == 0)
        {
            vramTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, vramTexture);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgb, 1024, 512, 0, PixelFormat.Rgb, PixelType.UnsignedByte, IntPtr.Zero);
        }
        if (vramDirty)
        {
            GL.BindTexture(TextureTarget.Texture2D, vramTexture);
            GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, 1024, 512, PixelFormat.Rgb, PixelType.UnsignedByte, vramRgb);
            vramDirty = false;
        }
    }






    public void Clear()
    {
        GL.ClearColor(0.1f, 0.1f, 0.6f, 1.0f);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
    }


    public void DrawFlatTriangle(Vertex2D v0, Vertex2D v1, Vertex2D v2, uint color)
    {
        // Apenas posição, cor é fixa no shader
        triangleVertices = new float[]
        {
            v0.X, v0.Y,
            v1.X, v1.Y,
            v2.X, v2.Y
        };
        vertexBuffer.SetData(triangleVertices);
        pipeline.Bind();
        GL.BindVertexArray(vao);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        GL.BindVertexArray(0);
        pipeline.Unbind();
    }


    public void Dispose()
    {
        pipeline?.Dispose();
        vertexBuffer?.Dispose();
        if (vao != 0) GL.DeleteVertexArray(vao);
    }

    public void DrawTexturedTriangle(TexVertex v0, TexVertex v1, TexVertex v2, int textureId)
    {
        // Não implementado neste exemplo mínimo
    }

    public int CreateTextureFromVRAM(ushort[,] vram, int x, int y, int width, int height)
    {
        // Não implementado neste exemplo mínimo
        return 0;
    }

    public void Present()
    {
        EnsureVRAMTexture();
        // Desenha a textura VRAM na tela inteira (viewport)
        // Shader simples para quad fullscreen
        GL.Disable(EnableCap.DepthTest);
        if (fullscreenQuad == 0)
            CreateFullscreenQuad();
        GL.UseProgram(quadProgram);
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, vramTexture);
        GL.BindVertexArray(fullscreenQuad);
        GL.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
        GL.BindVertexArray(0);
        GL.UseProgram(0);
    }

    // --- Fullscreen quad ---
    private int fullscreenQuad = 0;
    private int quadProgram = 0;
    private void CreateFullscreenQuad()
    {
        // Ajusta UV para mostrar só a área 320x240 do canto superior esquerdo da VRAM
        // O BIOS pode usar offset Y=16, então tente mostrar de (0,16) até (320,256)
        float u0 = 0f, v0 = 16f / 512f;
        float u1 = 320f / 1024f, v1 = 256f / 512f;
        float[] quadVerts = {
            -1, -1, u0, v0,
            +1, -1, u1, v0,
            -1, +1, u0, v1,
            +1, +1, u1, v1
        };
        fullscreenQuad = GL.GenVertexArray();
        int vbo = GL.GenBuffer();
        GL.BindVertexArray(fullscreenQuad);
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, quadVerts.Length * sizeof(float), quadVerts, BufferUsageHint.StaticDraw);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));
        GL.BindVertexArray(0);
        // Shader para quad
        string vs = "#version 330 core\nlayout(location=0) in vec2 pos;layout(location=1) in vec2 uv;out vec2 vUV;void main(){gl_Position=vec4(pos,0,1);vUV=uv;}";
        string fs = "#version 330 core\nin vec2 vUV;out vec4 FragColor;uniform sampler2D tex;void main(){FragColor=texture(tex,vUV);}";
        int vshader = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vshader, vs);
        GL.CompileShader(vshader);
        int fshader = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(fshader, fs);
        GL.CompileShader(fshader);
        quadProgram = GL.CreateProgram();
        GL.AttachShader(quadProgram, vshader);
        GL.AttachShader(quadProgram, fshader);
        GL.LinkProgram(quadProgram);
        GL.DeleteShader(vshader);
        GL.DeleteShader(fshader);
    }
}

// ... resto da classe no próximo passo
