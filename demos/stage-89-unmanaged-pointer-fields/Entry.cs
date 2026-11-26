using PointerFieldDependency;

namespace PointerFieldDemo
{
    public static unsafe class Kernels
    {
        public static int Run(int value)
        {
            var slot = new NativeSlot(value);
            if (slot.Address != null) return -1;
            slot.Address = null;
            void* previous = slot.Read();
            slot.Write(value == 0 ? null : previous);
            return IsNull(slot.Address) ? slot.Marker : -2;
        }

        private static bool IsNull(void* address) => address == null;
        private static void* Empty() => null;
        public static int ReadEmpty() => IsNull(Empty()) ? 1 : -1;
        public static int ReadNullSlot(NativeSlot slot) => IsNull(slot.Address) ? 1 : -1;
        // Since Stage 137 (decision 0060), void* is a native address: any value can be
        // stored, a null read raises NullReferenceException, and null exports as 0.
        public static int StoreAddress(int value)
        {
            var slot = new NativeSlot(value);
            slot.Address = (void*)1;
            return slot.Address == (void*)1 ? slot.Marker : -1;
        }
        public static int DereferenceNullAddress() => *(int*)new NativeSlot(0).Address;
        public static void* ExportNull() => null;
        public static int RejectCallbackLayout() => new CallbackSlot().Marker;
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
