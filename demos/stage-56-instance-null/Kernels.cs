using System;
using System.Threading;

namespace InstanceNull
{
    public sealed class Cell
    {
        public Cell Next;
        public EventHandler Handler;
        public int Value;

        // The receiver check is observable even though this body does not read this.
        public int Compute(int argument) => argument ^ 17;

        public int Prepare(int argument)
        {
            Value += 1;
            return checked(argument + 1);
        }

        public void Tick(object sender, EventArgs args) => Value += 5;

        public void Tock(object sender, EventArgs args) => Value += 7;
    }

    public static class Kernels
    {
        public static int Invoke(Cell target, int argument) => target.Compute(argument);

        public static int CallCleanup(int present, int argument)
        {
            var state = new Cell();
            if (present != 0) state.Next = new Cell();
            int result;
            try
            {
                try { result = Invoke(state.Next, state.Prepare(argument)); }
                finally { state.Value += 100; }
            }
            catch (NullReferenceException) { return -state.Value; }
            catch (OverflowException) { return -1000 - state.Value; }
            return result + state.Value;
        }

        public static int ExchangeCleanup(int present)
        {
            var state = new Cell();
            EventHandler oldValue = state.Tick;
            EventHandler replacement = state.Tock;
            if (present != 0)
            {
                state.Next = new Cell();
                state.Next.Handler = oldValue;
            }
            try
            {
                try { Exchange(state.Next, replacement, oldValue); }
                finally { state.Value += 100; }
            }
            catch (NullReferenceException) { return -state.Value; }
            state.Next.Handler(state, EventArgs.Empty);
            return state.Value;
        }

        private static void Exchange(Cell cell, EventHandler value, EventHandler comparand) =>
            Interlocked.CompareExchange(ref cell.Handler, value, comparand);
    }

    public static class InstanceDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] data, int index, int value) => data[index] = unchecked((byte)value);
        public static int ReadByte(byte[] data, int index) => data[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int Compile(byte[] image, int row, byte[] output) =>
            PortableCompiler.MethodCompiler.Compile(image, row, output);
    }
}
