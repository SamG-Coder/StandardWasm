namespace PointerFieldDependency
{
    public sealed unsafe class NativeSlot
    {
        public void* Address;
        public int Marker;

        public NativeSlot(int marker) => Marker = marker;
        public void Write(void* address) => Address = address;
        public void* Read() => Address;
    }

    public sealed unsafe class CallbackSlot
    {
        public delegate*<void> Callback;
        public int Marker;
    }
}
