using System;

namespace EventLibrary
{
    public static class EventDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] image, int index, int value) => image[index] = unchecked((byte)value);
        public static int ReadByte(byte[] image, int index) => image[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int Compile(byte[] image, int row, byte[] output) =>
            PortableCompiler.MethodCompiler.Compile(image, row, output);
    }

    public static class EventCases
    {
        public static int SubscriptionOrder()
        {
            var source = new EventSource();
            var log = new EventLog(source);
            source.Changed += log.First;
            source.Changed += log.Second;
            source.Changed += log.First;
            source.Raise();
            // A new delegate for the same target and method removes the last match.
            source.Changed -= log.First;
            source.Raise();
            return log.Value;
        }

        public static int RemoveDuringRaise()
        {
            var source = new EventSource();
            var log = new EventLog(source);
            source.Changed += log.RemoveSecond;
            source.Changed += log.Second;
            // The in-flight invocation list still includes Second on this raise.
            source.Raise();
            source.Raise();
            return log.Value;
        }

        public static int EmptyAndUnmatched()
        {
            var source = new EventSource();
            var log = new EventLog(source);
            source.Raise();
            source.Changed -= log.First;
            source.Changed += log.Second;
            source.Changed -= log.First;
            source.Raise();
            source.Changed -= log.Second;
            source.Raise();
            return log.Value;
        }

        public static int DistinctTargets()
        {
            var source = new EventSource();
            var first = new EventLog(source);
            var second = new EventLog(source);
            source.Changed += first.First;
            source.Changed += second.First;
            source.Changed -= first.First;
            source.Raise();
            return first.Value * 10 + second.Value;
        }
    }

    internal sealed class EventSource
    {
        internal event EventHandler Changed;

        internal void Raise() => Changed?.Invoke(this, EventArgs.Empty);
    }

    internal sealed class EventLog
    {
        private readonly EventSource source;
        internal int Value;

        internal EventLog(EventSource source) => this.source = source;

        internal void First(object sender, EventArgs args) => Record(1, sender, args);

        internal void Second(object sender, EventArgs args) => Record(2, sender, args);

        internal void RemoveSecond(object sender, EventArgs args)
        {
            Record(3, sender, args);
            source.Changed -= Second;
        }

        private void Record(int digit, object sender, EventArgs args)
        {
            // Both are identity checks. The sender must be the existing source object.
            if (sender != source || args != EventArgs.Empty)
                digit = 9;

            Value = Value * 10 + digit;
        }
    }
}
