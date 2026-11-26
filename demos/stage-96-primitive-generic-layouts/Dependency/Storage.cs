namespace PrimitiveGenericDependency
{
    public sealed class Label
    {
        public int Marker;
        public Label(int marker) => Marker = marker;
    }

    public struct Pair<TKey, TValue>
    {
        public TKey Key;
        public TValue Value;
        public TKey[] Keys;
        public TValue[] Values;
    }

    public sealed class Frame
    {
        public Pair<int, Label> Signed;
        public Pair<uint, Label> Unsigned;
        public Pair<int[], Label> SignedVectors;
        public Pair<uint[], Label> UnsignedVectors;
        public Pair<double, Label>[] Measures;
    }
}
