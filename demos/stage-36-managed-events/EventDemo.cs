using System;
using System.Threading;

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

    internal sealed class DelegateSlot
    {
        private Delegate handler;

        internal Delegate Update(Delegate next, bool remove)
        {
            handler = remove ? Delegate.Remove(handler, next) : Delegate.Combine(handler, next);
            return handler;
        }
    }

    public static class EventCases
    {
        public static int BaseDelegateStorage(int scenario)
        {
            var sender = new EventSource();
            var log = new EventLog(sender);
            var slot = new DelegateSlot();
            EventHandler first = log.First;
            EventHandler second = log.Second;
            Delegate original = slot.Update(first, false);
            if ((object)original != first) return -1;
            Delegate combined = slot.Update(second, false);
            if (scenario == 0)
                ((EventHandler)combined)(sender, EventArgs.Empty);
            else if (scenario == 1)
                ((EventHandler)slot.Update(first, true))(sender, EventArgs.Empty);
            else if (scenario == 2)
                return slot.Update(null, true) == null ? -2 : 1;
            else
            {
                slot.Update(first, true);
                return slot.Update(second, true) == null ? 1 : -3;
            }
            return log.Value;
        }

        public static int CompareHandlerField()
        {
            var sender = new EventSource();
            var log = new EventLog(sender);
            var slot = new HandlerSlot();
            EventHandler first = log.First;
            EventHandler equivalent = log.First;
            EventHandler replacement = log.Second;
            slot.Handler = first;
            if ((object)slot.Exchange(replacement, equivalent) != first || (object)slot.Handler != first)
                return -1;
            if ((object)slot.Exchange(replacement, first) != first || (object)slot.Handler != replacement)
                return -2;
            slot.Raise(sender, EventArgs.Empty);
            return log.Value;
        }

        public static int RemoveCallbacks(int scenario)
        {
            var sender = new EventSource();
            var log = new EventLog(sender);
            var slot = new HandlerSlot();
            EventHandler first = log.First;
            EventHandler second = log.Second;
            EventHandler third = log.Third;
            if (scenario == 0)
            {
                slot.Handler = first + second + third + first + second;
                EventHandler match = new EventHandler(log.First) + new EventHandler(log.Second);
                slot.Handler -= match;
                slot.Raise(sender, EventArgs.Empty);
                return log.Value;
            }
            if (scenario == 1)
            {
                EventHandler pair = first + second;
                EventHandler otherTarget = new EventLog(sender).First;
                if ((object)(pair - otherTarget) != pair) return -1;
                if ((object)(pair - third) != pair) return -2;
                if ((object)(pair - (first + second + third)) != pair) return -3;
                if ((object)(pair - first) != second) return -4;
                if ((object)(pair - second) != first) return -5;
                if (pair - (first + second) != null) return -6;
                EventHandler missing = slot.Handler;
                if ((object)(first - missing) != first) return -7;
                if (missing - first != null) return -8;
                return 1;
            }
            if (scenario == 2)
            {
                log.Slot = slot;
                slot.Handler = log.RemoveSecondFromSlot;
                slot.Handler += second;
                slot.Raise(sender, EventArgs.Empty);
                slot.Raise(sender, EventArgs.Empty);
                return log.Value;
            }
            slot.Handler = first + second + third;
            slot.Handler -= second;
            slot.Raise(sender, EventArgs.Empty);
            return log.Value;
        }

        public static int CombineCallbacks(int scenario)
        {
            var sender = new EventSource();
            var log = new EventLog(sender);
            var slot = new HandlerSlot();
            EventHandler first = log.First;
            EventHandler second = log.Second;
            EventHandler missing = slot.Handler;
            if (scenario == 0)
            {
                EventHandler original = first + second;
                slot.Handler = original + first;
                slot.Raise(sender, EventArgs.Empty);
                original(sender, EventArgs.Empty);
                return log.Value;
            }
            if (scenario == 1)
            {
                if (missing + missing != null) return -1;
                if ((object)(missing + first) != first) return -2;
                if ((object)(first + missing) != first) return -3;
                return 1;
            }
            // A callback adds a handler to the field. The current invocation
            // continues using the original list and sees the addition next time.
            log.Slot = slot;
            slot.Handler = log.AppendSecond;
            slot.Handler += first;
            slot.Raise(sender, EventArgs.Empty);
            slot.Raise(sender, EventArgs.Empty);
            return log.Value;
        }

        public static int InvokeBoundHandler(int scenario)
        {
            var sender = new EventSource();
            var first = new EventLog(sender);
            var second = new EventLog(sender);
            var slot = new HandlerSlot();
            slot.Handler = first.First;
            slot.Raise(sender, EventArgs.Empty);
            if (scenario == 0)
                slot.Handler = second.Second;
            else
                slot.Handler = first.Second;
            slot.Raise(sender, EventArgs.Empty);
            return first.Value * 10 + second.Value;
        }

        public static int InvokeEmptyHandler(int ignored)
        {
            var slot = new HandlerSlot();
            slot.Raise(new SenderNode(), EventArgs.Empty);
            return ignored;
        }

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

    internal sealed class HandlerSlot
    {
        internal EventHandler Handler;

        internal EventHandler Exchange(EventHandler replacement, EventHandler comparand) =>
            Interlocked.CompareExchange(ref Handler, replacement, comparand);

        internal void Raise(object sender, EventArgs args) => Handler(sender, args);
    }

    public static class SenderCases
    {
        public static int SenderIdentity(int scenario)
        {
            var sender = new SenderNode();
            var holder = new SenderHolder();
            if (holder.Sender != null) return -1;
            holder.Sender = sender;
            if (holder.Sender != sender) return -2;
            if (scenario == 0) return Equal(holder.Sender, Forward(sender));
            if (scenario == 1) return Equal(holder.Sender, new SenderNode());
            return Equal(holder.Sender, new SenderHolder());
        }

        private static object Forward(SenderNode sender) => sender;

        private static int Equal(object left, object right) => left == right ? 1 : 0;

        public static int SenderLocal(int replacements)
        {
            var sender = new SenderNode();
            object selected = sender;
            for (int i = 0; i < replacements; i++)
                selected = new SenderHolder();
            return Equal(sender, selected);
        }

        public static int ArgumentIdentity(int scenario)
        {
            var holder = new ArgumentHolder();
            if (holder.Args != null) return -1;
            holder.Args = EventArgs.Empty;
            if (scenario == 0) return SameArgs(holder.Args, EventArgs.Empty) ? 1 : 0;
            if (scenario == 1) return SameArgs(holder.Args, new EventArgs()) ? 1 : 0;
            if (scenario == 2) return SameArgs(new EventArgs(), new EventArgs()) ? 1 : 0;
            return Equal(EmptyArgument(), holder.Args);
        }

        private static bool SameArgs(EventArgs left, EventArgs right) => left == right;

        public static object EmptyArgument() => EventArgs.Empty;
    }

    internal sealed class SenderNode { }

    internal sealed class SenderHolder
    {
        internal object Sender;
    }

    internal sealed class ArgumentHolder
    {
        internal EventArgs Args;
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
        internal HandlerSlot Slot;

        internal EventLog(EventSource source) => this.source = source;

        internal void First(object sender, EventArgs args) => Record(1, sender, args);

        internal void Second(object sender, EventArgs args) => Record(2, sender, args);

        internal void Third(object sender, EventArgs args) => Record(3, sender, args);

        internal void AppendSecond(object sender, EventArgs args)
        {
            Record(3, sender, args);
            Slot.Handler += Second;
        }

        internal void RemoveSecondFromSlot(object sender, EventArgs args)
        {
            Record(4, sender, args);
            Slot.Handler -= Second;
        }

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
