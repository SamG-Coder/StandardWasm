using System;
using System.Threading;

namespace MulticastDelegateFixture
{
    public delegate void Notify(int value);
    public delegate void Alert(int value); // the same Invoke shape as Notify, a different type
    public delegate int Measure(int value);
    public delegate void PaintLike(object sender, Canvas canvas);

    public sealed class Canvas { public int Strokes; }

    // Field-like events: the C# compiler emits Delegate.Combine/Remove, castclass and an
    // Interlocked.CompareExchange<T> loop in add/remove.
    public sealed class Publisher
    {
        public event Notify Changed;
        public event Measure Measured;
        public void Raise(int value) => Changed?.Invoke(value);
        public int Ask(int value) => Measured == null ? -1 : Measured(value);
    }

    // The WinForms Control.Events shape: handlers kept as System.Delegate and retrieved with a
    // downcast to the event's own delegate type.
    public sealed class HandlerSlots
    {
        Delegate paint, notify;
        public void AddPaint(Delegate value) => paint = Delegate.Combine(paint, value);
        public void RemovePaint(Delegate value) => paint = Delegate.Remove(paint, value);
        public void AddNotify(Delegate value) => notify = Delegate.Combine(notify, value);
        public Delegate Paint => paint;
        public Delegate NotifyHandlers => notify;
    }

    public static class Kernels
    {
        static int trace;
        static void Push(int digit) => trace = trace * 10 + digit;
        static void One(int value) => Push(1);
        static void Two(int value) => Push(2);
        static void Three(int value) => Push(3);
        static void Fail(int value) => throw new InvalidOperationException();
        static int Double(int value) => value * 2;
        static int Square(int value) => value * value;
        static void Stroke(object sender, Canvas canvas) => canvas.Strokes += 1;
        static void BoldStroke(object sender, Canvas canvas) => canvas.Strokes += 10;

        static int Run(Notify handler)
        {
            trace = 0;
            if (handler != null) handler(0);
            return trace;
        }

        public static int InvocationOrder()
        {
            Notify n = One;
            n += Two;
            n += Three;
            return Run(n); // 123
        }

        public static int LastResultWins()
        {
            Measure m = Double;
            m += Square;
            return m(7); // 49
        }

        public static int RemoveDuplicateTakesLast()
        {
            Notify n = One;
            n += Two;
            n += One;
            n -= One; // removes the last One
            return Run(n); // 12
        }

        public static int RemoveLastSubsequence()
        {
            Notify one = One, two = Two;
            Notify n = one + two + one + two;
            n -= one + two; // the last occurrence of the pair
            return Run(n); // 12
        }

        public static int RemoveAbsentKeepsList()
        {
            Notify n = One;
            n += Two;
            n -= Three;
            return Run(n); // 12
        }

        public static int RemoveSoleLeavesNull()
        {
            Notify n = One;
            n -= One;
            return n == null ? 1 : 0;
        }

        public static int CombineWithNulls()
        {
            Notify none = null, one = One;
            Notify left = none + one, right = one + none, both = none + none;
            return (both == null ? 100 : 0) + Run(left) * 10 + Run(right); // 111
        }

        public static int ExceptionStopsInvocation()
        {
            Notify n = One;
            n += Fail;
            n += Two;
            trace = 0;
            try { n(0); }
            catch (InvalidOperationException) { return trace * 10 + 9; } // 19
            return -1;
        }

        public static int InstanceTargetsInOrder()
        {
            var first = new Canvas();
            var second = new Canvas();
            Measure m = v => first.Strokes += v;
            m += v => second.Strokes += v * 10;
            m(3);
            return first.Strokes * 100 + second.Strokes; // 330
        }

        public static int FieldLikeEvent()
        {
            var publisher = new Publisher();
            trace = 0;
            publisher.Changed += One;
            publisher.Changed += Two;
            publisher.Raise(0);
            publisher.Changed -= One;
            publisher.Raise(0);
            publisher.Changed -= Two;
            publisher.Raise(0);
            return trace; // 122
        }

        public static int FieldLikeFuncEvent()
        {
            var publisher = new Publisher();
            int before = publisher.Ask(5);
            publisher.Measured += Double;
            publisher.Measured += Square;
            return before * 1000 + publisher.Ask(5); // -975
        }

        public static int DelegateSlotDowncast()
        {
            var slots = new HandlerSlots();
            slots.AddPaint((PaintLike)Stroke);
            slots.AddPaint((PaintLike)BoldStroke);
            var canvas = new Canvas();
            ((PaintLike)slots.Paint)?.Invoke(null, canvas);
            slots.RemovePaint((PaintLike)Stroke);
            ((PaintLike)slots.Paint)?.Invoke(null, canvas);
            return canvas.Strokes; // 21
        }

        public static int DelegateSlotEmpty()
        {
            var slots = new HandlerSlots();
            var canvas = new Canvas();
            ((PaintLike)slots.Paint)?.Invoke(null, canvas);
            return slots.Paint == null ? 7 : 0;
        }

        public static int CastFromObject()
        {
            object o = (Notify)One;
            return Run((Notify)o); // 1
        }

        public static int WrongNominalCast()
        {
            object o = (Notify)One;
            try { var alert = (Alert)o; return alert == null ? -2 : -1; }
            catch (InvalidCastException) { return 8; }
        }

        public static int IsinstByNominalType()
        {
            object notify = (Notify)One, alert = (Alert)One, text = "x";
            return (notify is Notify ? 1 : 0) + (alert is Notify ? 10 : 0) + (alert is Alert ? 100 : 0) +
                (text is Notify ? 1000 : 0) + ((object)null is Notify ? 10000 : 0); // 101
        }

        public static int EventHandlerFromGeneralSlot()
        {
            var slots = new HandlerSlots();
            slots.AddNotify((Notify)One);
            try { var handler = (EventHandler)slots.NotifyHandlers; return handler == null ? -2 : -1; }
            catch (InvalidCastException) { return 6; }
        }

        public static int CombineMismatchRejected()
        {
            Delegate notify = (Notify)One, alert = (Alert)Two;
            try { Delegate.Combine(notify, alert); return -1; }
            catch (ArgumentException) { return 5; }
        }

        public static int RemoveMismatch()
        {
            Delegate notify = (Notify)One, alert = (Alert)One;
            try { return ReferenceEquals(Delegate.Remove(notify, alert), notify) ? 3 : 4; }
            catch (ArgumentException) { return 5; }
        }

        public static int GenericNominalKeys()
        {
            object ints = (Action<int>)(v => { }), unsigned = (Action<uint>)(v => { });
            return (ints is Action<int> ? 1 : 0) + (unsigned is Action<int> ? 10 : 0) + (unsigned is Action<uint> ? 100 : 0); // 101
        }

        static int Five() => 5;

        // Explicit boundaries (decision 0092): these must be diagnosed, not approximated.
        // Func<int> keeps decision 0088's single closure, so it has no invocation list.
        public static int RejectFuncIntCombine()
        {
            Func<int> five = Five;
            var combined = (Func<int>)Delegate.Combine(five, five);
            return combined();
        }

        // Delegate equality (op_Equality, Equals) compares targets and methods; not lowered yet.
        public static int RejectDelegateEquality()
        {
            Notify a = One, b = One;
            return a == b ? 1 : 0;
        }

        // A Func<int> closure is not an object reference yet (decision 0088), so it cannot reach a cast.
        public static int RejectFuncIntAsObject()
        {
            object five = (Func<int>)Five;
            return five is Notify ? 1 : 2;
        }

        sealed class Slot { public Notify Current; }

        // The field-like event accessor's identity exchange on an instance field.
        public static int ExchangeIdentity()
        {
            var slot = new Slot { Current = One };
            Notify replacement = Two, third = One, original = slot.Current;
            Notify seen = Interlocked.CompareExchange(ref slot.Current, replacement, original);
            Notify stale = Interlocked.CompareExchange(ref slot.Current, third, original);
            return (ReferenceEquals(seen, original) ? 1 : 0) + (ReferenceEquals(stale, replacement) ? 10 : 0) +
                Run(slot.Current) * 100; // 211
        }
    }
}
