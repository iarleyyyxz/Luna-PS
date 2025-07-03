namespace Luna.Math
{
    public struct Vertex2D
    {
        public float X, Y;

        public Vertex2D(float x, float y)
        {
            X = x;
            Y = y;
        }

        public override string ToString() => $"({X},{Y})";
    }
}