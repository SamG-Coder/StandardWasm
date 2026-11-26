using System;
using FrameworkDelegateDependency;

namespace FrameworkDelegateDemo
{
    // Each handler appends a digit, so the total records invocation order.
    public sealed class Recorder
    {
        public int Total;

        public void One(object sender, EventArgs e) => Total = unchecked(Total * 10 + 1);
        public void Two(object sender, EventArgs e) => Total = unchecked(Total * 10 + 2);
        public void Sender(object sender, EventArgs e) => Total = unchecked(Total * 10 + (sender is Publisher ? 3 : 4));
    }

    public static class Kernels
    {
        public static int Run(int value) =>
            unchecked(Subscribe(value) + Closure(value) * 3 + Combine(value) * 5 + Unsubscribe(value) * 7 + Identity(value) * 11);

        // Subscribe through a linked add_ accessor, then raise twice.
        public static int Subscribe(int value)
        {
            var publisher = new Publisher();
            var recorder = new Recorder();
            publisher.Raised += recorder.One;
            publisher.Raised += recorder.Sender;
            publisher.Raise();
            publisher.Raise();
            return unchecked(recorder.Total + value);
        }

        // A lambda over a local, as in the WinForms constructor's closure.
        public static int Closure(int value)
        {
            int hits = 0;
            var publisher = new Publisher();
            publisher.Raised += (sender, e) => hits = unchecked(hits + value);
            publisher.Raise();
            publisher.Raise();
            return hits;
        }

        // Combine keeps order. Remove takes the last matching occurrence and
        // ignores a missing handler.
        public static int Combine(int value)
        {
            var recorder = new Recorder();
            EventHandler one = recorder.One, two = recorder.Two;
            EventHandler handlers = one;
            handlers += two;
            handlers += one;
            handlers(null, EventArgs.Empty);
            handlers -= one;
            handlers(null, EventArgs.Empty);
            handlers -= new Recorder().One;
            handlers(null, EventArgs.Empty);
            return unchecked(recorder.Total * 3 + value);
        }

        // Removing through the linked remove_ accessor.
        public static int Unsubscribe(int value)
        {
            var publisher = new Publisher();
            var recorder = new Recorder();
            publisher.Raised += recorder.One;
            publisher.Raised += recorder.Two;
            publisher.Raised -= recorder.One;
            publisher.Raise();
            publisher.Raised -= recorder.Two;
            publisher.Raise();
            return unchecked(recorder.Total * 7 + value);
        }

        // Separately created delegates over the same target and method are
        // equal, so removing one removes the other. Another target is not.
        public static int Identity(int value)
        {
            var recorder = new Recorder();
            EventHandler first = recorder.Two;
            EventHandler second = recorder.Two;
            EventHandler removed = first;
            removed -= second;
            EventHandler kept = first;
            kept -= new Recorder().Two;
            return unchecked((removed == null ? 100 : 200) + (kept == null ? 10 : 20) + value);
        }

        public static int RejectStatic(int value)
        {
            EventHandler handler = Ignore;
            handler(null, EventArgs.Empty);
            return value;
        }

        private static void Ignore(object sender, EventArgs e) { }
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
