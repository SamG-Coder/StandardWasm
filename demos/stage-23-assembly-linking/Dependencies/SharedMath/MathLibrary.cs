namespace SharedMath
{
    public static class MathLibrary
    {
        public static int Scale(int value) => unchecked(value * 3);
        public static int Scale(uint value) => unchecked((int)value * 5);
        public static int Blend(int value) => LeafMath.Leaf.Offset(Scale(value));
    }
}
