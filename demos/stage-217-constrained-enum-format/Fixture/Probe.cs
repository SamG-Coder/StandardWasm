using System;
using System.Runtime.CompilerServices;

namespace ConstrainedEnumFormat
{
    public static class Probe
    {
        private enum I8 : sbyte { MinusOne = -1 }
        private enum U8 : byte { High = 128 }
        private enum I16 : short { MinusOne = -1 }
        private enum U16 : ushort { High = 32768 }
        private enum I32 : int { MinusOne = -1 }
        private enum U32 : uint { High = 0x80000000 }
        private enum I64 : long { MinusOne = -1 }
        private enum U64 : ulong { High = 0x8000000000000000 }
        private enum Empty : int { }
        private enum Letters : int { Éclair = 1, Жёлтый = 2 }
        private enum SmallSorted { Zero = 0, First = 1, Alias = 1, Two = 2 }
        private enum Unsorted { Two = 2, One = 1, MinusOne = -1, Zero = 0 }
        private enum UnsortedAlias { Two = 2, First = 1, Alias = 1 }
        [Flags] private enum Flags : byte { One = 1, Two = 2 }

        private enum LargeSorted
        {
            Value0 = 0, Value1 = 1, Value2 = 2, Value3 = 3,
            Value4 = 4, Value5 = 5, Value6 = 6, Value7 = 7,
            Value8 = 8, Value9 = 9, Value10 = 10, Value11 = 11,
            Value12 = 12, Value13 = 13, Value14 = 14, FirstAlias = 15, SecondAlias = 15, ThirdAlias = 15,
            Value16 = 16, Value17 = 17, Value18 = 18, Value19 = 19,
            Value20 = 20, Value21 = 21, Value22 = 22, Value23 = 23,
            Value24 = 24, Value25 = 25, Value26 = 26, Value27 = 27,
            Value28 = 28, Value29 = 29, Value30 = 30, Value31 = 31,
        }

        public static int Signed8Name() => TextCode((I8.MinusOne).ToString());
        public static int Signed8Min() => TextCode(((I8)sbyte.MinValue).ToString());
        public static int Unsigned8Name() => TextCode((U8.High).ToString());
        public static int Unsigned8Max() => TextCode(((U8)byte.MaxValue).ToString());
        public static int Signed16Name() => TextCode((I16.MinusOne).ToString());
        public static int Signed16Min() => TextCode(((I16)short.MinValue).ToString());
        public static int Unsigned16Name() => TextCode((U16.High).ToString());
        public static int Unsigned16Max() => TextCode(((U16)ushort.MaxValue).ToString());
        public static int Signed32Name() => TextCode((I32.MinusOne).ToString());
        public static int Signed32Min() => TextCode(((I32)int.MinValue).ToString());
        public static int Unsigned32Name() => TextCode((U32.High).ToString());
        public static int Unsigned32Max() => TextCode(((U32)uint.MaxValue).ToString());
        public static int Signed64Name() => TextCode((I64.MinusOne).ToString());
        public static int Signed64Min() => TextCode(((I64)long.MinValue).ToString());
        public static int Unsigned64Name() => TextCode((U64.High).ToString());
        public static int Unsigned64Max() => TextCode(((U64)ulong.MaxValue).ToString());
        public static int EmptyEnum() => TextCode(((Empty)0).ToString());
        public static int UnicodeName() => TextCode((Letters.Éclair).ToString());
        public static int UncombinedNames() => TextCode(((Letters)3).ToString());
        public static int SmallSortedAlias() => TextCode(((SmallSorted)1).ToString());
        public static int LargeSortedAlias() => TextCode(((LargeSorted)15).ToString());
        public static int UnsortedUnique() => TextCode(((Unsorted)1).ToString());

        public static int NameIdentity()
        {
            string name = Read8(I8.MinusOne);
            return ReferenceEquals(name, Read8(I8.MinusOne)) &&
                !ReferenceEquals(name, Read16(I16.MinusOne)) && !ReferenceEquals(name, "MinusOne") ? 1 : 0;
        }

        public static int DecimalIdentity() =>
            ReferenceEquals(Read8((I8)0), Read16((I16)0)) &&
            ReferenceEquals(Read16((I16)299), ReadUnsigned64((U64)299)) &&
            !ReferenceEquals(Read16((I16)300), Read16((I16)300)) &&
            !ReferenceEquals(Read8((I8)(-8)), Read8((I8)(-8))) ? 1 : 0;

        public static int GenericName() => TextCode(ReadGeneric(Letters.Жёлтый));

        // These calls deliberately require formatting outside this stage's scope.
        public static int RejectUnsortedAliases() => TextCode(ReadUnsortedAlias((UnsortedAlias)1));
        public static int RejectFlags() => TextCode(ReadFlags((Flags)3));

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string Read8(I8 value) => value.ToString();
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string Read16(I16 value) => value.ToString();
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string ReadUnsigned64(U64 value) => value.ToString();
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string ReadGeneric<T>(T value) where T : struct => value.ToString();
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string ReadUnsortedAlias(UnsortedAlias value) => value.ToString();
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string ReadFlags(Flags value) => value.ToString();

        // Export a scalar checksum so both engines compare the exact UTF-16 units.
        private static int TextCode(string value)
        {
            int checksum = 13;
            for (int i = 0; i < value.Length; i++)
                checksum = unchecked(31 * checksum + value[i]);
            return checksum;
        }
    }
}
