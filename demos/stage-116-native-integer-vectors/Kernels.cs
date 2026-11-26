namespace NativeIntegerVectors
{
    public static class Kernels
    {
        public static int Signed(int value)
        {
            var cells = new nint[2];
            cells[0] = value;
            return (int)cells[0] + (int)cells[1];
        }

        public static int Unsigned(int bits)
        {
            var cells = new nuint[1];
            cells[0] = unchecked((uint)bits);
            return unchecked((int)cells[0]);
        }

        public static int Bounds(int length, int index)
        {
            var cells = new nint[length];
            return (int)cells[index];
        }

        public static int NullLoad(int allocate)
        {
            nint[] cells = null;
            if (allocate != 0) cells = new nint[1];
            return (int)cells[0];
        }

        public static int ZeroStore(int value)
        {
            var cells = new nint[1];
            cells[0] = value;
            cells[0] = 0;
            return (int)cells[0];
        }

        public static nint[] MakeNative(int size) => new nint[size];

        public static int[] MakeFixed(int size) => new int[size];

        public static int ReadNative(nint[] cells) => (int)cells[0];

        public static int ReadFixed(int[] cells) => cells[0];
    }

    public static class AddressGuard
    {
        public static int ReadAddress(int index)
        {
            var cells = new nint[1];
            return ReadCell(ref cells[index]);
        }

        private static int ReadCell(ref nint cell) => (int)cell;
    }

    public sealed class Carrier
    {
        public nint[] Cells;

        public Carrier(int size) { Cells = new nint[size]; }
    }
}
