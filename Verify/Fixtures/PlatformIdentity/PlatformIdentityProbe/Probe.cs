extern alias ThirdParty;

namespace PlatformIdentityProbe
{
    public sealed class Cell : ThirdParty::System.Object
    {
        private int value;
        public Cell(int seed) => value = seed;
        public int Read() => value;
    }

    public sealed class EventSlot
    {
        public global::System.EventHandler Handler;
    }

    public static class Probe
    {
        public static int ObjectBase(int value) => new Cell(value).Read();

        public static int Concat() => ThirdParty::System.String.Concat("left", "right").Length;

        public static int InvokeFunc() => new ThirdParty::System.Func<int>(FuncValue).Invoke();

        private static int FuncValue() => 29;

        public static int EventArgsEmpty() => ThirdParty::System.EventArgs.Empty == null ? 0 : 1;

        public static int InvokeEvent()
        {
            ((ThirdParty::System.EventHandler)null).Invoke(null, null);
            return 0;
        }

        public static int Exchange()
        {
            var slot = new EventSlot();
            return ThirdParty::System.Threading.Interlocked.CompareExchange(ref slot.Handler, null, null) == null ? 0 : 1;
        }

        public static int Combine() => ThirdParty::System.Delegate.Combine(null, null) == null ? 0 : 1;
    }
}
