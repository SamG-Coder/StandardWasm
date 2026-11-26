namespace ValueMethodDependency
{
    // Read-only instance methods take the receiver's current aggregate.
    public struct Extent
    {
        public int Width;
        public int Height;

        public Extent(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public int Area => Width * Height;
        public bool IsEmpty => Width == 0 && Height == 0;

        public int Scaled(int factor, int offset) => Width * factor + Height - offset;

        public int Perimeter() => 2 * (Width + Height);

        // Calls on this pass the same receiver value onward.
        public int Describe() => Area * 3 + Perimeter();
    }

    // A value constructor may call a read-only method on its own receiver.
    public struct Summary
    {
        public int Left;
        public int Right;
        public int Total;

        public Summary(int left, int right)
        {
            Left = left;
            Right = right;
            Total = 0;
            int total = Sum();
            Total = total;
        }

        public int Sum() => Left + Right;
    }

    // Methods that write through their receiver need copy-out and stay diagnosed.
    public struct Counter
    {
        public int Value;

        public Counter(int value) { Value = value; }

        public void Store(int value) { Value = value; }
        public void Reset() { this = default; }
        public void Replace(int value) { this = new Counter(value); }
    }
}
