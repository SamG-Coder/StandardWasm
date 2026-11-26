namespace System
{
    public sealed class String
    {
        public String(char[] value) { }
        public int Length => 99;
        public static string Concat(string left, string right) => "third-party";
        public static int Concat(int left, int right) => unchecked(left * 3 - right);
    }

    public struct Char
    {
        public int Payload;
    }
}
