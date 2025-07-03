using OpenTK.Graphics.OpenGL4;
using System;
using Luna.Renderer;

namespace Luna.Renderer.OpenGL
{
    public class OpenGLPipeline : IPipeline
    {
        private readonly OpenGLShader _shader;
        public OpenGLPipeline(OpenGLShader shader)
        {
            _shader = shader;
        }
        public void Bind() => _shader.Bind();
        public void Unbind() => _shader.Unbind();
        public void SetUniform(string name, float value) => _shader.SetUniform(name, value);
        public void SetUniform(string name, float v0, float v1, float v2, float v3) => _shader.SetUniform(name, v0, v1, v2, v3);
        public void Dispose() { _shader.Dispose(); }
    }

    public class OpenGLShader : IShader
    {
        private int _program;
        public OpenGLShader(string vertexSrc, string fragmentSrc)
        {
            int vs = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vs, vertexSrc);
            GL.CompileShader(vs);
            int fs = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fs, fragmentSrc);
            GL.CompileShader(fs);
            _program = GL.CreateProgram();
            GL.AttachShader(_program, vs);
            GL.AttachShader(_program, fs);
            GL.LinkProgram(_program);
            GL.DeleteShader(vs);
            GL.DeleteShader(fs);
        }
        public void Bind() => GL.UseProgram(_program);
        public void Unbind() => GL.UseProgram(0);
        public int GetAttribLocation(string name) => GL.GetAttribLocation(_program, name);
        public void SetUniform(string name, float value) => GL.Uniform1(GL.GetUniformLocation(_program, name), value);
        public void SetUniform(string name, float v0, float v1, float v2, float v3) => GL.Uniform4(GL.GetUniformLocation(_program, name), v0, v1, v2, v3);
        public void Dispose() { if (_program != 0) GL.DeleteProgram(_program); _program = 0; }
    }

    public class OpenGLVertexBuffer : IVertexBuffer
    {
        private int _vbo;
        public OpenGLVertexBuffer()
        {
            _vbo = GL.GenBuffer();
        }
        public void Bind() => GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        public void Unbind() => GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        public void SetData(float[] data)
        {
            Bind();
            GL.BufferData(BufferTarget.ArrayBuffer, data.Length * sizeof(float), data, BufferUsageHint.DynamicDraw);
        }
        public void Dispose() { if (_vbo != 0) GL.DeleteBuffer(_vbo); _vbo = 0; }
    }

    public class OpenGLTexture : ITexture
    {
        private int _tex;
        public OpenGLTexture(int width, int height)
        {
            _tex = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _tex);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }
        public void Bind(int unit = 0)
        {
            GL.ActiveTexture(TextureUnit.Texture0 + unit);
            GL.BindTexture(TextureTarget.Texture2D, _tex);
        }
        public void Unbind() => GL.BindTexture(TextureTarget.Texture2D, 0);
        public void Dispose() { if (_tex != 0) GL.DeleteTexture(_tex); _tex = 0; }
    }
}
