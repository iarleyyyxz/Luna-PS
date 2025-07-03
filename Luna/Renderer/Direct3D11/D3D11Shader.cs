using System;
using Vortice.Direct3D11;

namespace Luna.Renderer.Direct3D11
{
    // Classe utilitária para gerenciamento de shaders Direct3D11
    public class D3D11Shader : IDisposable
    {
        public ID3D11VertexShader VertexShader { get; private set; }
        public ID3D11PixelShader PixelShader { get; private set; }
        public ID3D11InputLayout InputLayout { get; private set; }
        public ID3D11Device Device { get; private set; }

        public D3D11Shader(ID3D11Device device, byte[] vertexShaderBytecode, byte[] pixelShaderBytecode, InputElementDescription[] inputElements)
        {
            Device = device;
            VertexShader = device.CreateVertexShader(vertexShaderBytecode);
            PixelShader = device.CreatePixelShader(pixelShaderBytecode);
            InputLayout = device.CreateInputLayout(inputElements, vertexShaderBytecode);
        }

        public void Dispose()
        {
            InputLayout?.Dispose();
            PixelShader?.Dispose();
            VertexShader?.Dispose();
        }
    }

    // Estrutura de vértice para o shader simples
    public struct VertexShaderVertex
    {
        public float X, Y, Z;
        public float R, G, B, A;
        public VertexShaderVertex(float x, float y, float z, uint color)
        {
            X = x; Y = y; Z = z;
            // Extrai RGBA do uint
            R = ((color >> 0) & 0xFF) / 255.0f;
            G = ((color >> 8) & 0xFF) / 255.0f;
            B = ((color >> 16) & 0xFF) / 255.0f;
            A = ((color >> 24) & 0xFF) / 255.0f;
        }
    }
}
