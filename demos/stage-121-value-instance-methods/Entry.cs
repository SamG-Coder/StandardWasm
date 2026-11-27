using ValueMethodDependency;

namespace ValueMethodDemo
{
    // A value type in the entry assembly exercises the local (MethodDef) call path.
    public struct Span
    {
        public int Start;
        public int Length;

        public Span(int start, int length)
        {
            Start = start;
            Length = length;
        }

        public int End => Start + Length;
        public bool Contains(int value) => value >= Start && value < End;
    }

    public static class Kernels
    {
        public static int LinkedGetters(int width, int height)
        {
            var extent = new Extent(width, height);
            return extent.Area + (extent.IsEmpty ? 1000 : 0) + extent.Scaled(3, 7);
        }

        public static int LinkedChain(int width, int height)
        {
            var extent = new Extent(width, height);
            return extent.Describe();
        }

        // A copy keeps its value. The later in-place write is visible to the
        // next call on the original location only.
        public static int CopyThenWrite(int width, int height)
        {
            var extent = new Extent(width, height);
            Extent copy = extent;
            extent.Width = width + 5;
            return extent.Area - copy.Area * 2;
        }

        // The zero default is the null aggregate.
        public static int DefaultReceiver()
        {
            Extent extent = default;
            return extent.Area + (extent.IsEmpty ? 1 : 0) + extent.Perimeter();
        }

        public static int ConstructorCall(int left, int right)
        {
            var summary = new Summary(left, right);
            return summary.Total * 2 + summary.Sum();
        }

        public static int LocalSpan(int start, int length, int probe)
        {
            var span = new Span(start, length);
            return span.End * 10 + (span.Contains(probe) ? 1 : 0);
        }

        public static int RejectStore(int value)
        {
            var counter = new Counter(1);
            counter.Store(value);
            return counter.Value;
        }

        public static int ResetReceiver(int value)
        {
            var counter = new Counter(value);
            counter.Reset();
            return counter.Value;
        }

        public static int RejectReplace(int value)
        {
            var counter = new Counter(1);
            counter.Replace(value);
            return counter.Value;
        }
    }

    public static class Demo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] image, int index, int value) => image[index] = unchecked((byte)value);
        public static int ReadByte(byte[] image, int index) => image[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int CompileBundle(byte[] bundle, byte[] entries, byte[] output) =>
            PortableCompiler.MethodCompiler.CompileBundle(bundle, entries, output);
    }
}
