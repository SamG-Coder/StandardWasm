using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // Decision 0105: emission of proved alias-free array pins (PinnedArrays). `pins` holds
    // [pinned local, pointer local, vector, heap global] per pin site; the scratch array holds
    // NativeHeapWasm.ScratchCount i32 locals.
    internal static class PinnedArraysWasm
    {
        internal const int PinCapacity = 1600, UnpinCapacity = 400;

        internal static int CountPins(int[] records)
        {
            int count = 0;
            for (int slot = 0; slot < records.Length; slot += BranchIl.Width) if (records[slot] == BranchIl.PinArray) count++;
            return count;
        }

        // [] -> [] with `target` = the native address of the pinned copy of `array` (pin site `site`).
        // A second pin of the same array in this frame reuses the live block, so both pointers alias
        // as on the CLR.
        internal static int Pin(byte[] code, int used, int[] pins, int site, int array, int target, int[] s)
        {
            int vector = pins[site * 4 + 2], heap = pins[site * 4 + 3];
            code[used++] = 0x02; code[used++] = 0x40; // block: done
            for (int other = 0; other < pins.Length / 4; other++)
            {
                if (other == site || pins[other * 4] == pins[site * 4] || pins[other * 4 + 2] != vector) continue;
                // if (other != null && other == array && otherPointer != 0) { target = otherPointer; done }
                used = Local(code, used, 0x20, pins[other * 4]);
                code[used++] = 0xd1; code[used++] = 0x45; // non-null
                code[used++] = 0x04; code[used++] = 0x40;
                used = Local(code, used, 0x20, pins[other * 4]);
                used = Local(code, used, 0x20, array);
                code[used++] = 0xd3; // ref.eq
                used = Local(code, used, 0x20, pins[other * 4 + 1]);
                code[used++] = 0x45; code[used++] = 0x45; // the other pin's pointer is live (as 0/1)
                code[used++] = 0x71; // and
                code[used++] = 0x04; code[used++] = 0x40;
                used = Local(code, used, 0x20, pins[other * 4 + 1]);
                used = Local(code, used, 0x21, target);
                code[used++] = 0x0c; code[used++] = 2; // br done
                code[used++] = 0x0b;
                code[used++] = 0x0b;
            }
            int size = ElementBytes(vector);
            // bytes = max(1, length * size)
            used = Local(code, used, 0x20, array);
            code[used++] = 0xfb; code[used++] = 0x0f; // array.len
            used = Const(code, used, size);
            code[used++] = 0x6c; // i32.mul
            used = Local(code, used, 0x22, s[NativeHeapWasm.Size]);
            code[used++] = 0x45;
            code[used++] = 0x04; code[used++] = 0x40;
            used = Const(code, used, 1);
            used = Local(code, used, 0x21, s[NativeHeapWasm.Size]);
            code[used++] = 0x0b;
            used = Const(code, used, 16);
            used = Local(code, used, 0x21, s[NativeHeapWasm.Alignment]);
            used = NativeHeapWasm.Allocate(code, used, heap, s, false);
            used = Copy(code, used, vector, array, s[NativeHeapWasm.Result], s[NativeHeapWasm.Extra], true);
            used = Local(code, used, 0x20, s[NativeHeapWasm.Result]);
            used = Local(code, used, 0x21, target);
            code[used++] = 0x0b; // done
            return used;
        }

        // [] -> []: copies the pin of site `site` back into its array and frees the block, unless
        // another live pin of the frame shares the block. With `clear`, the pinned local becomes
        // null (ret and exception exits, where no IL store follows).
        internal static int Unpin(byte[] code, int used, int[] pins, int site, int[] s, bool clear)
        {
            int local = pins[site * 4], pointer = pins[site * 4 + 1], vector = pins[site * 4 + 2], heap = pins[site * 4 + 3];
            used = Local(code, used, 0x20, local);
            code[used++] = 0xd1; code[used++] = 0x45;
            used = Local(code, used, 0x20, pointer);
            code[used++] = 0x45; code[used++] = 0x45; // holding an address (as 0/1)
            code[used++] = 0x71; // pinned and holding an address
            code[used++] = 0x04; code[used++] = 0x40;
            code[used++] = 0x02; code[used++] = 0x40; // block: shared
            for (int other = 0; other < pins.Length / 4; other++)
            {
                if (pins[other * 4] == local || pins[other * 4 + 2] != vector) continue;
                used = Local(code, used, 0x20, pins[other * 4]);
                code[used++] = 0xd1; code[used++] = 0x45;
                code[used++] = 0x04; code[used++] = 0x40;
                used = Local(code, used, 0x20, pins[other * 4]);
                used = Local(code, used, 0x20, local);
                code[used++] = 0xd3;
                used = Local(code, used, 0x20, pins[other * 4 + 1]);
                used = Local(code, used, 0x20, pointer);
                code[used++] = 0x46; // same block
                code[used++] = 0x71;
                code[used++] = 0x0d; code[used++] = 1; // br_if shared: the other pin owns the block
                code[used++] = 0x0b;
            }
            used = Copy(code, used, vector, local, pointer, s[NativeHeapWasm.Extra], false);
            used = NativeHeapWasm.Release(code, used, heap, pointer, s);
            // The block is gone: a later store to the pinned local must not copy into it again.
            used = Const(code, used, 0);
            used = Local(code, used, 0x21, pointer);
            code[used++] = 0x0b; // shared
            if (clear)
            {
                code[used++] = 0xd0; // ref.null of the vector's heap type
                used = WasmBinary.Unsigned(code, used, WasmBinary.HeapType(vector));
                used = Local(code, used, 0x21, local);
            }
            code[used++] = 0x0b;
            return used;
        }

        // Copies every element between `array` and the block at `address` (into the block when
        // `toBlock`). `index` is an i32 scratch local.
        private static int Copy(byte[] code, int used, int vector, int array, int address, int index, bool toBlock)
        {
            int size = ElementBytes(vector);
            used = Const(code, used, 0);
            used = Local(code, used, 0x21, index);
            code[used++] = 0x02; code[used++] = 0x40; // block
            code[used++] = 0x03; code[used++] = 0x40; // loop
            used = Local(code, used, 0x20, index);
            used = Local(code, used, 0x20, array);
            code[used++] = 0xfb; code[used++] = 0x0f;
            code[used++] = 0x4f; // i32.ge_u
            code[used++] = 0x0d; code[used++] = 1;
            if (toBlock)
            {
                used = Element(code, used, address, index, size);
                used = Local(code, used, 0x20, array);
                used = Local(code, used, 0x20, index);
                used = Gc(code, used, vector == Bytes || vector == Chars ? 0x0d : 0x0b, vector); // array.get(_u)
                used = Store(code, used, vector);
            }
            else
            {
                used = Local(code, used, 0x20, array);
                used = Local(code, used, 0x20, index);
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
