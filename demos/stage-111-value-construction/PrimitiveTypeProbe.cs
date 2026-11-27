using System;

namespace ValueConstructionDemo
{
    public static class PrimitiveTypeProbe
    {
        private static bool IsIntType<T>() => typeof(T) == typeof(int);
        private static bool IsByteType<T>() => typeof(T) == typeof(byte);

        public static int CheckPrimitiveDescriptors(int value)
        {
            if (IsIntType<int>()) value = unchecked(value * 7 + 3);
            if (IsIntType<uint>()) value = -1;
            if (IsByteType<byte>()) value = unchecked(value * 3 + 7);
            if (IsByteType<bool>()) value = -2;
            return value;
        }

        private static int ClassifyClosedType<T>(int value)
        {
            if (typeof(T).IsValueType) return unchecked(value + 17);
            return unchecked(value - 3);
        }

        public static int CheckClosedValueType(int value) =>
            unchecked(ClassifyClosedType<int>(value) * 5 + ClassifyClosedType<string>(value));

        private struct TypeSlot<T>
        {
            private readonly int enabled;
            public TypeSlot(int enabled) => this.enabled = enabled;
            public Type Read() => enabled == 0 ? null : typeof(T);
        }

        private sealed class TypeCarrier<T>
        {
            public Type Read() => typeof(T);
        }

        private sealed class TypeMarker { }

        public static int CheckSpecializedTypeResults(int mode)
        {
            var signed = new TypeSlot<int>(mode);
            var unsigned = new TypeSlot<uint>(mode);
            Type returned = signed.Read();
            int result = returned == typeof(int) ? 1 : 0;
            if (unsigned.Read() == typeof(int)) result |= 2;
            if (unsigned.Read() == typeof(uint)) result |= 4;
            if (returned == null) result |= 8;
            if (new TypeCarrier<TypeMarker>().Read() == typeof(TypeMarker)) result |= 16;
            return result;
        }

        private static int PrimitiveHash<T>(T value) => value.GetHashCode();
        private static int PrimitiveHashReference<T>(ref T value) => value.GetHashCode();

        public static int CheckPrimitiveHash(int value)
        {
            var cells = new int[1];
            cells[0] = unchecked(value * 3 + 7);
            return unchecked(PrimitiveHash(value) * 5 + PrimitiveHash((uint)value) * 11 +
                PrimitiveHashReference(ref cells[0]));
        }
    }
}
