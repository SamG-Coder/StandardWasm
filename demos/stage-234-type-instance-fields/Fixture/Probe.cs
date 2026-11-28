using System;

namespace TypeInstanceFields
{
    public sealed class Slot
    {
        public readonly Type Value;
        public Slot() { }
        public Slot(Type value) { Value = value; }
    }

    public sealed class MutableSlot
    {
        public Type Value;
        public MutableSlot(Type value) { Value = value; }
    }

    public readonly struct ValueSlot
    {
        public readonly Type Value;
        public ValueSlot(Type value) { Value = value; }
    }

    public static class Probe
    {
        public static int Identity() => new Slot(typeof(Slot)).Value == typeof(Slot) ? 811 : 0;

        public static int SeparateInstances()
        {
            var first = new Slot(typeof(Slot));
            var second = new Slot(new MutableSlot(null).GetType());
            return first.Value == typeof(Slot) && second.Value == typeof(MutableSlot) ? 823 : 0;
        }

        public static int NullAndDefault()
        {
            var initialized = new Slot(typeof(Slot));
            return initialized.Value == typeof(Slot) && new Slot(null).Value == null &&
                new Slot().Value == null ? 827 : 0;
        }

        private static ValueSlot MakeSlot(Type value) => new ValueSlot(value);

        public static int StructCopy()
        {
            var original = MakeSlot(typeof(Slot));
            var copy = original;
            original = MakeSlot(typeof(ValueSlot));
            return copy.Value == typeof(Slot) && original.Value == typeof(ValueSlot) ? 831 : 0;
        }

        public static int MutableDiagnostic()
        {
            var slot = new MutableSlot(typeof(Slot));
            return slot == null ? 0 : 829;
        }
    }
}
