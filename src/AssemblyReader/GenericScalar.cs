namespace PeLibrary
{
    internal static class GenericScalar
    {
        // Closed signatures keep the CLI element identity even when the same
        // Wasm field or vector type stores multiple primitive types.
        private const int ScalarBase = 0x200, VectorBase = 0x240, ElementMask = 0x1f;

        internal static int Key(int element) =>
            element >= 2 && element <= 0x0d || NativeStorage.IsIntegerElement(element) ? ScalarBase + element : 0;

        internal static bool IsScalar(int key) => (key & ~ElementMask) == ScalarBase;

        internal static int VectorKey(int scalar)
        {
            int element = scalar & ElementMask;
            return ArrayTypeRefs.Storage(element) != 0 ? VectorBase + element : -2;
        }

        internal static int Storage(int key)
        {
            int element = key & ElementMask;
            if ((key & ~ElementMask) == VectorBase) return ArrayTypeRefs.Storage(element);
            if (!IsScalar(key)) return key;
            if (element <= 7) return NarrowStorage.FromElement(element);
            if (element == 8 || element == 9) return 1; // CLI int32 stack category.
            if (WideStorage.IsIntegerElement(element)) return WideStorage.Scalar;
            if (NativeStorage.IsIntegerElement(element)) return NativeStorage.Scalar;
            return FloatStorage.FromElement(element);
        }
    }
}
