using System;
using Luna.Renderer;

namespace Luna.Renderer.Direct3D11
{
    // Stub: implementação real depende do backend D3D11
    public class D3D11Pipeline : IPipeline
    {
        public D3D11Shader Shader { get; private set; }
        public D3D11Pipeline(D3D11Shader shader)
        {
            this.Shader = shader;
        }
        public void Bind()
        {
            // Bind shaders e input layout
            if (Shader == null) return;
            var deviceContext = Shader.Device.ImmediateContext;
            deviceContext.IASetInputLayout(Shader.InputLayout);
            deviceContext.VSSetShader(Shader.VertexShader);
            deviceContext.PSSetShader(Shader.PixelShader);
        }

        public void Unbind()
        {
            if (Shader == null) return;
            var deviceContext = Shader.Device.ImmediateContext;
            deviceContext.IASetInputLayout(null);
            deviceContext.VSSetShader(null);
            deviceContext.PSSetShader(null);
        }

        public void SetUniform(string name, float value) { /* TODO */ }
        public void SetUniform(string name, float v0, float v1, float v2, float v3) { /* TODO */ }
        public void Dispose()
        {
            Shader?.Dispose();
        }
    }
    // Implemente D3D11Shader, D3D11Texture, D3D11VertexBuffer conforme necessário
}
