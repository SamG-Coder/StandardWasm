using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class CharacterSpanSearchWasm
    {
        internal const int Capacity = 224;

        // Search the evaluated view's current UTF-16 range. A negative
        // second local index means the caller supplied just one needle.
        internal static int Find(byte[] code, int used, int view, int first, int second,
            int result, int length, int index, bool nativeViews)
        {
            used = NarrowNeedle(code, used, first);
            if (second >= 0) used = NarrowNeedle(code, used, second);
            used = Local(code, SpanWasm.Length(code, used, view), 0x21, length);
            used = Local(code, Const(code, used, 0), 0x21, index);
            code[used++] = 0x02; code[used++] = 0x40; // search block
            code[used++] = 0x03; code[used++] = 0x40; // loop
            used = Local(code, used, 0x20, index);
            used = Local(code, used, 0x20, length);
            code[used++] = 0x4f; // i32.ge_u
            code[used++] = 0x04; code[used++] = 0x40;
            used = Local(code, Const(code, used, -1), 0x21, result);
            code[used++] = 0x0c; code[used++] = 2; // exhausted
            code[used++] = 0x0b;

            used = Read(code, used, view, index, nativeViews);
            if (second >= 0) used = Local(code, used, 0x22, result);
            used = Local(code, used, 0x20, first);
            code[used++] = 0x46; // i32.eq
            if (second >= 0)
            {
                used = Local(code, used, 0x20, result);
                used = Local(code, used, 0x20, second);
                code[used++] = 0x46;
                code[used++] = 0x72; // i32.or
            }
            code[used++] = 0x04; code[used++] = 0x40;
            used = Local(code, Local(code, used, 0x20, index), 0x21, result);
            code[used++] = 0x0c; code[used++] = 2; // first match
            code[used++] = 0x0b;
            used = Local(code, used, 0x20, index);
            used = Const(code, used, 1);
            code[used++] = 0x6a;
            used = Local(code, used, 0x21, index);
            code[used++] = 0x0c; code[used++] = 0;
            code[used++] = 0x0b; // loop
            code[used++] = 0x0b; // search block
            return used;
        }

        private static int NarrowNeedle(byte[] code, int used, int needle)
        {
            used = Local(code, used, 0x20, needle);
            used = Const(code, used, 0xffff);
            code[used++] = 0x71; // i32.and
            return Local(code, used, 0x21, needle);
        }

        private static int Read(byte[] code, int used, int view, int index, bool nativeViews)
        {
            if (nativeViews)
            {
                used = SpanWasm.LoadField(code, used, view, 0);
                code[used++] = 0xd1; // a null owner denotes native backing
                code[used++] = 0x04; code[used++] = 0x7f;
                used = NativeCharacterViewWasm.GuardBase(code, used, view);
                used = NativeCharacterViewWasm.ReadWithinSpan(code, used, view, index);
                code[used++] = 0x05;
            }
            used = SpanWasm.CheckedArrayAddress(code, used, Chars, view, index);
            used = Gc(code, used, 0x0d, Chars); // unsigned packed UTF-16
            if (nativeViews) code[used++] = 0x0b;
            return used;
        }
    }
}
