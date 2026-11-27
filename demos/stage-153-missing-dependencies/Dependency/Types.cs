namespace MissingDependency
{
    // The runner and the oracle both leave this assembly out (issue #299).
    public class Widget
    {
        public int Value = 7;
        public static int Twice(int x) => x * 2;
    }

    public class BaseThing { public int Base = 1; }

    public struct Point { public int X; public int Y; }
}
