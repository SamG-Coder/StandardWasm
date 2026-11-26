namespace BeforeFieldInitDependency
{
    public static class Journal
    {
        public static int Value;

        public static int Record(int value)
        {
            Value = value;
            return value;
        }
    }

    public static class Relaxed
    {
        public static int State = Journal.Record(42);
        public static int Skip(int value) => unchecked(value + Journal.Value);
        public static int Read() => State;
    }

    public sealed class RelaxedInstance
    {
        public static int State = Journal.Record(31);
        private int result;

        public RelaxedInstance(int value) => result = unchecked(value + Journal.Value);
        public int Read() => unchecked(result + 1);
    }

    public static class Precise
    {
        static Precise() => Journal.Record(73);
        public static int Skip(int value) => unchecked(value + Journal.Value);
    }
}
