using System;

namespace ManagedObjectByrefs
{
    public static class Kernels
    {
        private static void Write(ref object slot, object value) => slot = value;
        private static object Read(ref object slot) => slot;

        public static int AcrossCall(int index)
        {
            var vector = new object[2];
            var marker = new object();
            Write(ref vector[index], marker);
            if (Read(ref vector[index]) != marker) return -1;
            return vector[1 - index] == null ? 7 : -2;
        }

        private static object WriteThenRead(ref object target, ref object alias, object value)
        {
            target = value;
            return alias;
        }

        public static int SharedElement(int writeIndex, int readIndex)
        {
            var vector = new object[2];
            var marker = new object();
            object result = WriteThenRead(ref vector[writeIndex], ref vector[readIndex], marker);
            if (result == marker) return 31;
            return result == null ? 19 : -1;
        }

        public static int LocalAlias(int useOther)
        {
            var first = new object[1];
            var other = new object[1];
            ref object location = ref first[0];
            if (useOther != 0) location = ref other[0];
            var marker = new object();
            Write(ref location, marker);
            if (Read(ref location) != marker) return -1;
            if (first[0] == marker && other[0] == null) return 41;
            return other[0] == marker && first[0] == null ? 43 : -2;
        }

        private static void WriteThenDivide(ref object target, object value, int divisor)
        {
            target = value;
            if (10 / divisor == 1) target = null;
        }

        public static int DuringUnwind(int divisor)
        {
            var vector = new object[1];
            var marker = new object();
            try { WriteThenDivide(ref vector[0], marker, divisor); }
            catch (DivideByZeroException) { return vector[0] == marker ? 23 : -1; }
            return vector[0] == marker ? 29 : 17;
        }

        private static object Mark(object[] trace)
        {
            object marker = new object();
            trace[0] = marker;
            return marker;
        }

        public static int AddressChecks(int index, int useNull)
        {
            var trace = new object[1];
            object[] vector = useNull == 0 ? new object[2] : null;
            try { Write(ref vector[index], Mark(trace)); }
            catch (NullReferenceException) { return trace[0] == null ? 11 : -1; }
            catch (IndexOutOfRangeException) { return trace[0] == null ? 13 : -2; }
            return vector[index] != null && vector[index] == trace[0] ? 37 : -3;
        }

        private sealed class Marker
        {
            internal int Value;
        }

        private static void Create(out Marker slot, int value)
        {
            slot = new Marker { Value = value };
        }

        private static int ClearAndRead(ref Marker target, ref Marker alias)
        {
            target = null;
            return alias == null ? -1 : alias.Value;
        }

        public static int TypedLocal(int value, int share)
        {
            Create(out Marker first, value);
            Marker other = first;
            ref Marker alias = ref other;
            if (share != 0) alias = ref first;
            int observed = ClearAndRead(ref first, ref alias);
            return first == null && other.Value == value ? observed : -2;
        }

        private static int ReplaceParameter(Marker input, int value)
        {
            Create(out input, value);
            return input.Value;
        }

        public static int TypedParameter(int value)
        {
            var marker = new Marker { Value = 17 };
            int observed = ReplaceParameter(marker, value);
            return marker.Value == 17 ? observed : -1;
        }
    }

    // These methods are CLR-callable, but a managed location stays inside
    // compiler-verified managed code. They are excluded from the demo exports.
    public static class HostBoundary
    {
        public static void HostAlias(ref object value) => value = null;
    }

    public static class TypedBoundary
    {
        private static void WriteString(ref string value) => value = null;

        public static int TypedElement()
        {
            var source = new string[] { "value" };
            WriteString(ref source[0]);
            return source[0] == null ? 1 : 0;
        }
    }
}
