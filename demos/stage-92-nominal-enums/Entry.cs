using EnumFieldDependency;

namespace EnumFieldDemo
{
    public sealed class DerivedFrame : Frame
    {
        public enum Local : ushort { Zero }
        public Local LocalState;
    }

    public static class Kernels
    {
        public static int Run(int value)
        {
            var frame = new DerivedFrame();
            if (frame.State != States.None || frame.Flags != ByteFlags.None) return -1;
            frame.State = unchecked((States)value);
            frame.Flags = Rules.Echo(unchecked((ByteFlags)value));
            frame.Signed = unchecked((SignedByte)value);
            frame.Short = unchecked((SignedShort)value);
            frame.UnsignedShort = unchecked((UnsignedShort)value);
            frame.LocalState = unchecked((DerivedFrame.Local)value);
            int flags = (frame.Flags & ByteFlags.High) != 0 ? 23 : 0;
            return unchecked(Rules.Score(frame.State) + flags + (int)frame.Signed * 3 + (int)frame.Flags * 5 +
                (int)frame.Short * 7 + (int)frame.UnsignedShort * 11 + (int)frame.LocalState * 13);
        }

        public static long WidenWord(int value)
        {
            var frame = new Frame { Word = unchecked((UnsignedWord)(uint)value) };
            return (long)frame.Word;
        }

        public static long RoundTripWide(long value)
        {
            var frame = new Frame
            {
                Long = unchecked((SignedLong)value),
                UnsignedLong = unchecked((UnsignedLong)(ulong)value)
            };
            return unchecked((long)frame.Long ^ ((long)frame.UnsignedLong >> 32));
        }

        public static int Read(Frame frame) => (int)frame.State;
        public static int ScoreOther(int value) => Rules.Score(unchecked((OtherStates)value));
        public static object RejectBox(int value) => unchecked((States)value);
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
