using SequentialLayoutDependency;

namespace SequentialLayoutDemo
{
    public static class Kernels
    {
        public static int SealedArea(int width, int height) => new Rectangle(1, width, height).Area();

        // Abstract dispatch through the sequential base.
        public static int BaseArea(int width, int height)
        {
            Shape shape = new Rectangle(1, width, height);
            return shape.Area();
        }

        // A virtual override that calls the sequential base implementation.
        public static int BaseWeight(int id, int width)
        {
            Shape shape = new Rectangle(id, width, 1);
            return shape.Weight();
        }

        // Narrow, wide, embedded value, reference and static fields across the link.
        public static int LedgerFields(int seed, int amount)
        {
            var ledger = new Ledger();
            ledger.Tag = unchecked((byte)seed);
            ledger.Total = (long)amount * 3000000;
            Pair pair = default;
            pair.Left = seed;
            pair.Right = amount;
            ledger.Last = pair;
            ledger.Owner = new Rectangle(seed & 15, 2, 3);
            Ledger.Mark = amount - seed;
            pair.Left = 0;
            // The stored copy keeps its own value after the local changes.
            return unchecked((int)(ledger.Total >> 8)) ^ ledger.Tag + ledger.Last.Left - ledger.Last.Right + pair.Left +
                ledger.Owner.Id * 1000 + Ledger.Mark;
        }

        // A fresh sequential instance has zeroed fields.
        public static int DefaultLedger()
        {
            var ledger = new Ledger();
            return ledger.Tag + (int)ledger.Total + ledger.Last.Left + ledger.Last.Right + (ledger.Owner == null ? 1 : 0);
        }

        // A sequential class derived from a sequential class.
        public static int AccountFields(int seed, int extra)
        {
            var account = new Account { Extra = extra };
            Ledger ledger = account;
            Pair pair = default;
            pair.Right = seed;
            ledger.Last = pair;
            ledger.Owner = new Rectangle(7, seed, extra);
            return ledger.Last.Right * 3 - account.Extra + ledger.Owner.Id;
        }

        public static int RejectFromAuto(int value) => new SequentialFromAuto { Extra = value }.Extra;
        public static int RejectInitialized(int value) => new Initialized { Value = value }.Value + Initialized.Base;
        public static int RejectPacked(int value) => new Packed { Value = value }.Value;
        public static int RejectSized(int value) => new Sized { Value = value }.Value;
        public static int RejectOverlaid(int value) => new Overlaid { Value = value }.Value;
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
