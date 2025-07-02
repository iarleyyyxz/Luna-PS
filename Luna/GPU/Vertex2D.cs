namespace Luna.Math
{
    public struct Vertex2D
    {
        private float x, y;

        public float X { get => x; set => x = value; }
        public float Y { get => y; set => y = value; }

        public Vertex2D(float x, float y)
        {
            this.x = x;
            this.y = y;
        }
    }
}