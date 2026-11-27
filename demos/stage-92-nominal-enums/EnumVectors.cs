using System;

namespace EnumFieldDemo
{
    public static partial class Kernels
    {
        private enum Small : sbyte { }
        private enum Word : ushort { }
        private enum Mode { }
        private enum OtherMode { }
        private enum Wide : long { }

        private static int ReadMode(Mode[] items, int index) => (int)items[index];
        private static int ReadOther(OtherMode[] items, int index) => (int)items[index];

        public static int EnumVectors(int scenario)
        {
            var values = new Mode[2];
            values[0] = (Mode)(-41);
            var other = new OtherMode[2];
            other[1] = (OtherMode)19;
            if (scenario == 0) return values.Length + ReadMode(values, 0) + ReadMode(values, 1) + ReadOther(other, 1);
            if (scenario == 1)
            {
                var small = new Small[1];
                small[0] = (Small)(-91);
                var words = new Word[1];
                words[0] = (Word)60001;
                return (int)small[0] + (int)words[0];
            }
            if (scenario == 2)
            {
                var wide = new Wide[1];
                wide[0] = (Wide)0x12345678abcdef01L;
                return (int)((long)wide[0] >> 32);
            }
            try
            {
                return ReadMode(scenario == 3 ? null : values, values.Length);
            }
            catch (NullReferenceException) { return -7; }
            catch (IndexOutOfRangeException) { return -11; }
        }
    }
}
