using System;
using Vortice.Direct3D11;
using Luna.Renderer;

namespace Luna.Renderer.Direct3D11
{
// D3D11Shader está definido em D3D11Shader.cs, não duplicar aqui

    public class D3D11VertexBuffer : IVertexBuffer
    {
        public ID3D11Buffer Buffer { get; private set; }
        private ID3D11Device device;
        public D3D11VertexBuffer(ID3D11Device device, byte[] data, int size)
        {
            this.device = device;
            var desc = new Vortice.Direct3D11.BufferDescription((uint)size, Vortice.Direct3D11.BindFlags.VertexBuffer, Vortice.Direct3D11.ResourceUsage.Default);
            Buffer = device.CreateBuffer((ReadOnlySpan<byte>)data, desc);
        }
        public void Bind() { /* Bind handled in renderer for now */ }
        public void Unbind() { }
        public void SetData(float[] data) { /* Not implemented for immutable buffer */ }
        public void Dispose() { Buffer?.Dispose(); }
    }

}
