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
