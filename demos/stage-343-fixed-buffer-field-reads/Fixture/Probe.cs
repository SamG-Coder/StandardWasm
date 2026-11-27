namespace FixedBufferReadFixture
{
    public static unsafe class Probe
    {
        private struct Header
        {
#pragma warning disable CS0649 // The native-memory case writes Stamp through its byte layout.
            internal int Stamp;
#pragma warning restore CS0649
            internal fixed char Text[4];
        }

        private struct UnsignedWord
        {
            internal ushort Value;
        }

        public static int DefaultValue()
        {
            Header header = default;
            return header.Text[0];
        }

        public static int ManagedSnapshot()
        {
            byte* bytes = stackalloc byte[sizeof(Header)];
            *(int*)bytes = 37;
            *(char*)(bytes + sizeof(int)) = '\uffff';
            Header header = *(Header*)bytes;
            *(char*)(bytes + sizeof(int)) = 'x';
            return header.Text[0] + header.Stamp;
        }

        public static int NativeReceiver()
        {
            byte* bytes = stackalloc byte[sizeof(Header)];
            *(char*)(bytes + sizeof(int)) = '\u8123';
            return ((Header*)bytes)->Text[0];
        }

        public static int UnsignedValue()
        {
            var carrier = new UnsignedWord { Value = 54321 };
            ref ushort field = ref carrier.Value;
            return field;
        }
    }
}
