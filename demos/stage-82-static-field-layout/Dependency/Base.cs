namespace FieldLayoutBase
{
    public class Record
    {
        public static object SharedTag;
        public int First;
        public const int Scale = 7;
        public static int SharedCount;
        public int Last;

        public Record(int value)
        {
            First = unchecked(value + 1);
            Last = unchecked(value + 2);
        }
    }
}
