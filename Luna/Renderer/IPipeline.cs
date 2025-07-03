namespace Luna.Renderer
{
    public interface IPipeline : System.IDisposable
    {
        void Bind();
        void Unbind();
        void SetUniform(string name, float value);
        void SetUniform(string name, float v0, float v1, float v2, float v3);
        // Outros métodos para uniforms, textures, etc.
    }

    public interface IShader : System.IDisposable
    {
        void Bind();
        void Unbind();
        int GetAttribLocation(string name);
    }

    public interface ITexture : System.IDisposable
    {
        void Bind(int unit = 0);
        void Unbind();
    }

    public interface IVertexBuffer : System.IDisposable
    {
        void Bind();
        void Unbind();
        void SetData(float[] data);
    }
}
