using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class CharacterSpanCopyWasm
    {
        internal const int Capacity = 512;

        internal static int Copy(byte[] code, int used, int source, int destination, int length, int index, bool nativeViews)
        {
            used = SpanWasm.Length(code, used, source);
            used = Local(code, used, 0x22, length);
            used = SpanWasm.Length(code, used, destination);
            code[used++] = 0x4b; // Source must fit in destination.
            used = ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.ArgumentTag);
            used = Local(code, used, 0x20, length);
            code[used++] = 0x04; code[used++] = 0x40; // Empty copies need no backing owner.
            if (nativeViews)
            {
                used = SpanWasm.LoadField(code, used, source, 0);
                code[used++] = 0xd1;
                code[used++] = 0x04; code[used++] = 0x40;
                used = SpanWasm.LoadField(code, used, destination, 0);
                code[used++] = 0xd1;
                code[used++] = 0x04; code[used++] = 0x40;
                used = NativeCharacterViewWasm.GuardBase(code, used, source);
                used = NativeCharacterViewWasm.GuardBase(code, used, destination);
                used = SpanWasm.LoadField(code, used, destination, 1);
                used = SpanWasm.LoadField(code, used, source, 1);
                used = Local(code, used, 0x20, length);
                used = Const(code, used, 1);
                code[used++] = 0x74; // UTF-16 bytes
                code[used++] = 0xfc; code[used++] = 0x0a; code[used++] = 0; code[used++] = 0; // memory.copy
                code[used++] = 0x05;
                used = MixedCopy(code, used, source, destination, length, index, true);
                code[used++] = 0x0b;
                code[used++] = 0x05;
                used = SpanWasm.LoadField(code, used, destination, 0);
                code[used++] = 0xd1;
                code[used++] = 0x04; code[used++] = 0x40;
                used = MixedCopy(code, used, source, destination, length, index, false);
                code[used++] = 0x05;
            }
            used = ArrayBase(code, used, destination);
            used = ArrayBase(code, used, source);
            used = Local(code, used, 0x20, length);
            used = Gc(code, used, 0x11, Chars); // array.copy preserves overlap.
            used = WasmBinary.Unsigned(code, used, WasmBinary.HeapType(Chars));
            if (nativeViews) { code[used++] = 0x0b; code[used++] = 0x0b; }
            code[used++] = 0x0b;
            return used;
        }

        private static int MixedCopy(byte[] code, int used, int source, int destination, int length, int index, bool nativeSource)
        {
            // The address spaces are disjoint, so the copy can run forward.
            int nativeView = nativeSource ? source : destination;
            used = NativeCharacterViewWasm.GuardBase(code, used, nativeView);
            used = Const(code, used, 0);
            used = Local(code, used, 0x21, index);
            code[used++] = 0x02; code[used++] = 0x40;
            code[used++] = 0x03; code[used++] = 0x40;
            used = Local(code, used, 0x20, index);
            used = Local(code, used, 0x20, length);
            code[used++] = 0x46;
            code[used++] = 0x0d; code[used++] = 1;
            if (nativeSource)
            {
                used = SpanWasm.CheckedArrayAddress(code, used, Chars, destination, index);
                used = NativeCharacterViewWasm.ReadWithinSpan(code, used, source, index);
                used = Gc(code, used, 0x0e, Chars);
            }
            else
            {
                used = NativeCharacterViewWasm.LinearAddress(code, used, destination, index);
                used = SpanWasm.CheckedArrayAddress(code, used, Chars, source, index);
                used = Gc(code, used, 0x0d, Chars);
                used = LinearMemoryWasm.Store(code, used, 0x53);
            }
            used = Local(code, used, 0x20, index);
            used = Const(code, used, 1);
            code[used++] = 0x6a;
            used = Local(code, used, 0x21, index);
            code[used++] = 0x0c; code[used++] = 0;
            code[used++] = 0x0b; code[used++] = 0x0b;
            return used;
        }

        private static int ArrayBase(byte[] code, int used, int span)
        {
            used = SpanWasm.LoadField(code, used, span, 0);
            code[used++] = 0xfb; code[used++] = 0x16;
            used = WasmBinary.Signed(code, used, WasmBinary.HeapType(Chars));
            return SpanWasm.LoadField(code, used, span, 1);
        }
    }
}
