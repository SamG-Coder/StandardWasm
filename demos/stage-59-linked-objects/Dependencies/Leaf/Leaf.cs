namespace ObjectLeaf
{
    public sealed class LeafCell
    {
        public string Text;
        public byte[] Data;

        public LeafCell(int value)
        {
            Text = "leaf";
            Data = new byte[1];
            Data[0] = unchecked((byte)value);
        }

        public int Score() => Text.Length + Data[0] + 11;
    }

    public static class Leaf
    {
        public static int Compute(int value) => new LeafCell(value).Score();
        public static object Make(int value) => new LeafCell(value);
        public static object Echo(object value) => value;
    }
}
