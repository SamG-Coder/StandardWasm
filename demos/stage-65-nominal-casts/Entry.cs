using System;
using CastControls;

namespace NominalCasts
{
    public sealed class Button
    {
        public int Value;

        public Button(int value) => Value = value;
    }

    public sealed class Other
    {
        public int Value;

        public Other(int value) => Value = value;
    }

    internal sealed class ClickSource
    {
        internal int Value;
        internal event EventHandler Click;

        internal ClickSource(int value) => Value = value;
        internal void Raise() => Click?.Invoke(this, EventArgs.Empty);
    }

    internal sealed class ClickObserver
    {
        internal int Value;

        internal void Receive(object sender, EventArgs args)
        {
            var button = sender as ClickSource;
            Value = button == null ? -1 : button.Value;
        }
    }

    public static class Kernels
    {
        public static int Run(int value) => unchecked(ReadEventSender(value) + ReadAs(2, value) + ReadForeignAs(3, value));

        public static int ReadAs(int kind, int value)
        {
            object sender = Sender(kind, value);
            var button = sender as Button;
            return button == null ? -1 : button.Value;
        }

        public static int ReadCast(int kind, int value)
        {
            object sender = Sender(kind, value);
            var button = (Button)sender;
            return button == null ? -1 : button.Value;
        }

        public static int ReadForeignAs(int kind, int value)
        {
            object sender = Sender(kind, value);
            var button = sender as RemoteButton;
            return button == null ? -1 : button.Value;
        }

        public static int CatchForeignCast(int kind, int value)
        {
            try { return ((RemoteButton)Sender(kind, value)).Value; }
            catch (InvalidCastException) { return -2; }
            catch (NullReferenceException) { return -3; }
        }

        public static int IsForeign(object sender, int value)
        {
            if (sender is RemoteButton) return value;
            return -17;
        }

        public static bool IsButton(int kind) => Sender(kind, 7) is Button;

        public static int ReadEventSender(int value)
        {
            var button = new ClickSource(value);
            var observer = new ClickObserver();
            button.Click += observer.Receive;
            button.Raise();
            return observer.Value;
        }

        private static object Sender(int kind, int value)
        {
            if (kind == 1) return new Button(value);
            if (kind == 2) return new Other(value);
            if (kind == 3) return new RemoteButton(value);
            return null;
        }
    }

    public static class LinkDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] data, int index, int value) => data[index] = unchecked((byte)value);
        public static int ReadByte(byte[] data, int index) => data[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int CompileBundle(byte[] bundle, byte[] entries, byte[] output) =>
            PortableCompiler.MethodCompiler.CompileBundle(bundle, entries, output);
    }
}
