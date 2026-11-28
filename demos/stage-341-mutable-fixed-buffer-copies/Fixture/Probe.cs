using System.Runtime.InteropServices;

namespace FixedBufferCopyFixture
{
    public static unsafe class Probe
    {
        private const int ElementCount = 128;
        private const int TextOffset = sizeof(int);
        private const int TailOffset = TextOffset + ElementCount * sizeof(char);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct Record
        {
            internal int Stamp;
            internal fixed char Data[ElementCount];
            internal int Tail;
        }

        public static int DefaultLocal()
        {
            Record value = default;
            value.Stamp = 71;
            return value.Stamp + value.Tail;
        }

        public static int CopyAllElements()
        {
            byte* source = stackalloc byte[sizeof(Record)];
            byte* destination = stackalloc byte[sizeof(Record)];
            *(int*)source = 17;
            char* text = (char*)(source + TextOffset);
            for (int index = 0; index < ElementCount; index++)
                text[index] = (char)(index * 257);
            *(int*)(source + TailOffset) = 21;

            Record snapshot = *(Record*)source;
            text[ElementCount - 1] = 'X';
            *(Record*)destination = snapshot;
            char* copied = (char*)(destination + TextOffset);
            for (int index = 0; index < ElementCount; index++)
                if (copied[index] != (char)(index * 257)) return -1;
            return *(int*)destination + *(int*)(destination + TailOffset);
        }

        public static int CopyAddressTakenLocal()
        {
            Record original = default;
            char* text = (char*)((byte*)&original + TextOffset);
            text[0] = 'A';
            text[ElementCount - 1] = 'Z';
            Record second = original;
            text[ElementCount - 1] = 'X';
            char* copied = (char*)((byte*)&second + TextOffset);
            return copied[0] * 1000 + copied[ElementCount - 1] * 10 + text[ElementCount - 1];
        }
    }
}
