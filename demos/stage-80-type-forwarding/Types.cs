namespace Forwarded
{
    public sealed class Box
    {
        public int Value;
        public Box(int value) => Value = value;
        public int Read() => Value;
    }

    public static class Operations
    {
        public static Box Identity(Box value) => value;
    }
}
