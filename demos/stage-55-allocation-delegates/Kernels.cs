using System;

namespace AllocationDelegates
{
    public sealed class Target
    {
        public EventHandler Callback;
        public int Value;

        public void Tick(object sender, EventArgs args) => Value += 5;

        public int Read() => Value;

        public EventArgs Prepare(int argument)
        {
            Value += 1;
            Value += checked(argument + 1);
            return EventArgs.Empty;
        }
    }

    public static class Kernels
    {
        public static int AllocateBytes(int length) => new byte[length].Length;

        public static int AllocateWide(int length) => new long[length].Length;

        public static int AllocationCleanup(int length)
        {
            int trace = 1;
            try
            {
                try { trace += AllocateBytes(length); }
                finally { trace = trace * 10 + 7; }
            }
            catch (OverflowException) { return -trace; }
            return trace;
        }

        public static int InvokeScalar(Func<int> callback) => callback();

        public static int BoundScalar(int value)
        {
            var target = new Target { Value = value };
            return InvokeScalar(target.Read);
        }

        public static int EventCleanup(int subscribed, int argument)
        {
            var target = new Target();
            if (subscribed != 0) target.Callback = target.Tick;
            try
            {
                try { target.Callback(target, target.Prepare(argument)); }
                finally { target.Value += 100; }
            }
            catch (NullReferenceException) { return -target.Value; }
            catch (OverflowException) { return -target.Value - 1000; }
            return target.Value;
        }

        public static int NullSafeEvent(int subscribed)
        {
            var target = new Target();
            if (subscribed != 0) target.Callback = target.Tick;
            target.Callback?.Invoke(target, target.Prepare(2));
            return target.Value;
        }
    }

    public static class TriggerDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] data, int index, int value) => data[index] = unchecked((byte)value);
        public static int ReadByte(byte[] data, int index) => data[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int Compile(byte[] image, int row, byte[] output) =>
            PortableCompiler.MethodCompiler.Compile(image, row, output);
    }
}
