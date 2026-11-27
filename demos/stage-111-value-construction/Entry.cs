using System;
using ValueConstructionDependency;

namespace ValueConstructionDemo
{
    public struct Pair
    {
        public int First;
        public int Second;

        public Pair(int first, int second)
        {
            First = first;
            Second = second;
        }

        // Chained local constructor, then a read-modify-write of this.Second.
        public Pair(int both) : this(both, unchecked(both * 2))
        {
            Second = unchecked(Second + 1);
        }

        // Only First is written. Second keeps the zero default.
        public Pair(long first) : this()
        {
            First = unchecked((int)first);
        }
    }

    public sealed class Holder
    {
        public Pair Value;

        public Holder(int value)
        {
            Value = new Pair(value);
        }
    }

    public struct Rejected
    {
        public int Value;

        // Assign writes through its receiver, which needs copy-out (decision 0049).
        public Rejected(int value) : this()
        {
            Assign(value);
        }

        private void Assign(int value) => Value = value;
    }

    internal sealed class LayoutState
    {
        public int Trace;
    }

    internal readonly ref struct LayoutScope
    {
        private readonly LayoutState state;
        private readonly nuint cookie;

        internal LayoutScope(LayoutState state, nuint cookie)
        {
            this.state = state;
            this.cookie = cookie;
        }

        public void Dispose()
        {
            if (cookie != 0) state.Trace = unchecked(state.Trace + 7);
        }
    }

    public static class Kernels
    {
        public static int DisposeScoped(int value)
        {
            var state = new LayoutState();
            var scope = new LayoutScope(state, unchecked((nuint)value));
            try
            {
                using (scope)
                {
                    state.Trace = checked(value + 1);
                }
            }
            catch (OverflowException)
            {
                return state.Trace + 100;
            }
            return state.Trace;
        }

        public static int Run(int value) =>
            unchecked(Defaults(value) + Fields(value) * 3 + Copies(value) * 5 + Chained(value) * 7);

        public static int Defaults(int value)
        {
            var pair = new Pair((long)value);
            var extent = new Extent(value, 3, 0);
            return unchecked(pair.First + pair.Second * 7 + extent.Width * 11 + extent.Height * 13);
        }

        public static int Fields(int value)
        {
            var pair = new Pair(value, 5);
            var extent = new Extent(value, -value);
            return unchecked(pair.First * 3 + pair.Second * 5 + extent.Width * 7 + extent.Height * 11);
        }

        public static int Copies(int value)
        {
            var holder = new Holder(value);
            Pair before = holder.Value;
            holder.Value = new Pair(unchecked(value + 1), 9);
            Pair local = new Pair(value, 2);
            Pair copy = local;
            local = new Pair(unchecked(value - 1), 4);
            local.First = unchecked(local.First + 100);
            Extent extent = new Extent(value);
            Extent extentCopy = extent;
            extent = new Extent(1, 2);
            return unchecked(before.First * 3 + before.Second * 5 + holder.Value.First * 7 + holder.Value.Second * 11 +
                copy.First * 13 + copy.Second * 17 + local.First * 19 + local.Second * 23 +
                extentCopy.Width * 29 + extentCopy.Height * 31 + extent.Width * 37 + extent.Height * 41);
        }

        public static int Chained(int value)
        {
            var pair = new Pair(value);
            var extent = new Extent(value);
            var panel = new Panel(value);
            return unchecked(pair.First * 3 + pair.Second * 5 + extent.Width * 7 + extent.Height * 11 +
                panel.Size.Width * 13 + panel.Size.Height * 17);
        }

        // Passed as an argument, the value comes from newobj rather than a local.
        public static int Argument(int value) => Measure(new Extent(value, 4), new Pair(value));

        private static int Measure(Extent extent, Pair pair) =>
            unchecked(extent.Width * 3 + extent.Height * 5 + pair.First * 7 + pair.Second * 11);

        public static int RejectReceiverCall(int value) => new Rejected(value).Value;

        // Roslyn constructs with newobj inside a protected range.
        public static int Guarded(int value)
        {
            Pair pair;
            try
            {
                pair = new Pair(value, 1);
            }
            catch (OverflowException)
            {
                return -1;
            }
            return pair.First;
        }

        public static int BorrowExtent(int value)
        {
            var extent = new Extent(value, 1);
            Borrow.Touch(ref extent, 3);
            return extent.Width;
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
