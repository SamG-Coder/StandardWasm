using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // Decision 0105: emission of proved alias-free pins (PinnedArrays). `pins` holds
    // [pinned local, block local, pinned type, heap global] per pin site. The pinned type is the
    // primitive array vector, or a byte span type for an array-backed span pin. The block local
    // is a compiler-owned i32 per site holding the live heap block (0 when the site is not
    // pinned), so pointer arithmetic on the user's pointer variable never affects the copy-back.
    // The scratch array holds NativeHeapWasm.ScratchCount i32 locals.
    internal static class PinnedArraysWasm
    {
        internal const int PinCapacity = 1800, UnpinCapacity = 460;

        internal static int CountPins(int[] records)
        {
            int count = 0;
            for (int slot = 0; slot < records.Length; slot += BranchIl.Width)
                if (records[slot] == BranchIl.PinArray || records[slot] == BranchIl.PinByteSpan) count++;
            return count;
        }

        internal static bool IsSpanPin(int type) => IsSpan(type);

        // [] -> [] with `target` = the native address of the pinned copy of `source` (pin site
        // `site`). A live pin of the same array (or of the same span local) in this frame lends its
        // block, so both pointers alias as on the CLR. An empty span pins to null, as
        // GetPinnableReference returns a null reference for it.
        internal static int Pin(byte[] code, int used, int[] pins, int site, int source, int target, int[] s)
        {
            int type = pins[site * 4 + 2], heap = pins[site * 4 + 3], block = pins[site * 4 + 1];
            bool span = IsSpanPin(type);
            code[used++] = 0x02; code[used++] = 0x40; // block: done
            for (int other = 0; other < pins.Length / 4; other++)
            {
                if (pins[other * 4 + 2] != type) continue;
                // if (other != null && other == source && otherBlock != 0) { block = target = otherBlock; done }
                used = Local(code, used, 0x20, pins[other * 4]);
                code[used++] = 0xd1; code[used++] = 0x45; // non-null
                code[used++] = 0x04; code[used++] = 0x40;
                used = Local(code, used, 0x20, pins[other * 4]);
                used = Local(code, used, 0x20, source);
                code[used++] = 0xd3; // ref.eq
                used = Local(code, used, 0x20, pins[other * 4 + 1]);
                code[used++] = 0x45; code[used++] = 0x45; // the other pin's block is live (as 0/1)
                code[used++] = 0x71; // and
                code[used++] = 0x04; code[used++] = 0x40;
                used = Local(code, used, 0x20, pins[other * 4 + 1]);
                used = Local(code, used, 0x22, block);
                used = Local(code, used, 0x21, target);
                code[used++] = 0x0c; code[used++] = 2; // br done
                code[used++] = 0x0b;
                code[used++] = 0x0b;
            }
            int size = span ? 1 : ElementBytes(type);
            // count = elements in view; an empty span (or a null one) pins to address 0.
            used = Count(code, used, type, source);
            used = Local(code, used, 0x21, s[NativeHeapWasm.Extra]);
            if (span)
            {
                used = Local(code, used, 0x20, s[NativeHeapWasm.Extra]);
                code[used++] = 0x45; // i32.eqz
                code[used++] = 0x04; code[used++] = 0x40;
                used = Const(code, used, 0);
                used = Local(code, used, 0x22, block);
                used = Local(code, used, 0x21, target);
                code[used++] = 0x0c; code[used++] = 1; // br done
                code[used++] = 0x0b;
            }
            // bytes = max(1, count * size)
            used = Local(code, used, 0x20, s[NativeHeapWasm.Extra]);
            if (size != 1)
            {
                used = Const(code, used, size);
                code[used++] = 0x6c; // i32.mul
            }
            used = Local(code, used, 0x22, s[NativeHeapWasm.Size]);
            code[used++] = 0x45;
            code[used++] = 0x04; code[used++] = 0x40;
            used = Const(code, used, 1);
            used = Local(code, used, 0x21, s[NativeHeapWasm.Size]);
            code[used++] = 0x0b;
            used = Const(code, used, 16);
            used = Local(code, used, 0x21, s[NativeHeapWasm.Alignment]);
            used = NativeHeapWasm.Allocate(code, used, heap, s, false);
            used = Local(code, used, 0x20, s[NativeHeapWasm.Result]);
            used = Local(code, used, 0x21, block);
            used = Copy(code, used, type, source, block, s[NativeHeapWasm.Extra], true);
            used = Local(code, used, 0x20, block);
            used = Local(code, used, 0x21, target);
            code[used++] = 0x0b; // done
            return used;
        }

        // [] -> []: ends pin site `site`. Its block is copied back into the array and freed unless
        // another live site of the frame still shares it; either way the site stops being live.
        // With `clear`, the pinned local becomes null (ret, exception exits and span unpins, where
        // no IL store of the local follows).
        internal static int Unpin(byte[] code, int used, int[] pins, int site, int[] s, bool clear)
        {
            int local = pins[site * 4], block = pins[site * 4 + 1], type = pins[site * 4 + 2], heap = pins[site * 4 + 3];
            used = Local(code, used, 0x20, local);
            code[used++] = 0xd1; code[used++] = 0x45;
            used = Local(code, used, 0x20, block);
            code[used++] = 0x45; code[used++] = 0x45; // holding a block (as 0/1)
            code[used++] = 0x71; // pinned and holding a block
            code[used++] = 0x04; code[used++] = 0x40;
            code[used++] = 0x02; code[used++] = 0x40; // block: shared
            for (int other = 0; other < pins.Length / 4; other++)
            {
                if (other == site || pins[other * 4 + 2] != type) continue;
                used = Local(code, used, 0x20, pins[other * 4]);
                code[used++] = 0xd1; code[used++] = 0x45;
                code[used++] = 0x04; code[used++] = 0x40;
                used = Local(code, used, 0x20, pins[other * 4]);
                used = Local(code, used, 0x20, local);
                code[used++] = 0xd3;
                used = Local(code, used, 0x20, pins[other * 4 + 1]);
                used = Local(code, used, 0x20, block);
                code[used++] = 0x46; // same block
                code[used++] = 0x71;
                code[used++] = 0x0d; code[used++] = 1; // br_if shared: the other site owns the block now
                code[used++] = 0x0b;
            }
            used = Copy(code, used, type, local, block, s[NativeHeapWasm.Extra], false);
            used = NativeHeapWasm.Release(code, used, heap, block, s);
            code[used++] = 0x0b; // shared
            // This site no longer holds the block: a later store or exit must not copy it again.
            used = Const(code, used, 0);
            used = Local(code, used, 0x21, block);
            if (clear)
            {
                code[used++] = 0xd0; // ref.null of the pinned type's heap type
                used = WasmBinary.Signed(code, used, WasmBinary.HeapType(type));
                used = Local(code, used, 0x21, local);
            }
            code[used++] = 0x0b;
            return used;
        }

        // [] -> [elements in view]: array.len, or the span's length (0 for a null span).
        private static int Count(byte[] code, int used, int type, int source)
        {
            if (IsSpanPin(type)) return SpanWasm.Length(code, used, source);
            used = Local(code, used, 0x20, source);
            code[used++] = 0xfb; code[used++] = 0x0f; // array.len
            return used;
        }

        // Copies every element between the pinned source and the block at `address` (into the block
        // when `toBlock`). `index` is an i32 scratch local. A span copies its own range of the owner.
        private static int Copy(byte[] code, int used, int type, int source, int address, int index, bool toBlock)
        {
            bool span = IsSpanPin(type);
            int vector = span ? Bytes : type;
            int size = ElementBytes(vector);
            used = Const(code, used, 0);
            used = Local(code, used, 0x21, index);
            code[used++] = 0x02; code[used++] = 0x40; // block
            code[used++] = 0x03; code[used++] = 0x40; // loop
            used = Local(code, used, 0x20, index);
            used = Count(code, used, type, source);
            code[used++] = 0x4f; // i32.ge_u
            code[used++] = 0x0d; code[used++] = 1;
            if (toBlock)
            {
                used = Element(code, used, address, index, size);
                used = Owner(code, used, type, source, index);
                used = Gc(code, used, vector == Bytes || vector == Chars ? 0x0d : 0x0b, vector); // array.get(_u)
                used = Store(code, used, vector);
            }
            else
            {
                used = Owner(code, used, type, source, index);
                used = Element(code, used, address, index, size);
                used = Load(code, used, vector);
                used = Gc(code, used, 0x0e, vector); // array.set
            }
            used = Local(code, used, 0x20, index);
            used = Const(code, used, 1);
            code[used++] = 0x6a;
            used = Local(code, used, 0x21, index);
            code[used++] = 0x0c; code[used++] = 0;
            code[used++] = 0x0b;
            code[used++] = 0x0b;
            return used;
        }

        // [] -> [owner array, element index]: the array itself and `index`, or the span's backing
        // byte array and its start + `index`.
        private static int Owner(byte[] code, int used, int type, int source, int index)
        {
            if (!IsSpanPin(type))
            {
                used = Local(code, used, 0x20, source);
                return Local(code, used, 0x20, index);
            }
            used = SpanWasm.LoadField(code, used, source, 0);
            code[used++] = 0xfb; code[used++] = 0x17; // ref.cast (ref null $bytes)
            used = WasmBinary.Signed(code, used, WasmBinary.HeapType(Bytes));
            used = SpanWasm.LoadField(code, used, source, 1);
            used = Local(code, used, 0x20, index);
            code[used++] = 0x6a;
            return used;
        }

        // [] -> [address + index * size]
        private static int Element(byte[] code, int used, int address, int index, int size)
        {
            used = Local(code, used, 0x20, address);
            used = Local(code, used, 0x20, index);
            if (size != 1)
            {
                used = Const(code, used, size);
                code[used++] = 0x6c;
            }
            code[used++] = 0x6a;
            return used;
        }

        private static int Store(byte[] code, int used, int vector)
        {
            code[used++] = vector == Bytes ? (byte)0x3a : vector == Chars ? (byte)0x3b : vector == Longs ? (byte)0x37 :
                vector == Singles ? (byte)0x38 : vector == Doubles ? (byte)0x39 : (byte)0x36;
            code[used++] = 0; // alignment 1: block addresses are 16-aligned, but keep it conservative
            code[used++] = 0;
            return used;
        }

        private static int Load(byte[] code, int used, int vector)
        {
            code[used++] = vector == Bytes ? (byte)0x2d : vector == Chars ? (byte)0x2f : vector == Longs ? (byte)0x29 :
                vector == Singles ? (byte)0x2a : vector == Doubles ? (byte)0x2b : (byte)0x28;
            code[used++] = 0;
            code[used++] = 0;
            return used;
        }

        private static int ElementBytes(int vector) => PinnedArrays.ElementBytes(vector);
    }
}
