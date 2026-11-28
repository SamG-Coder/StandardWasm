using static PortableCompiler.BranchWasm;

namespace PortableCompiler
{
    // Decision 0104: the NativeMemory heap, emitted into the module itself over memory 0.
    //
    // The first allocation grows memory by one page for a control block whose address the heap
    // global keeps (0 = not yet created): 32 free-list heads at [0, 128), the bump pointer at 128
    // and the bump limit at 132. Pages the heap grows belong to it alone, apart from the shadow
    // stack (decision 0065) and host-owned resource pages, because each owner takes its pages
    // with its own memory.grow.
    //
    // A block is a power-of-two size class of at least 32 bytes. The address handed out is
    // aligned as requested and preceded by 8 header bytes: the block start, then the class. Free
    // pushes the block on its class list (the link is the block's first word). Realloc keeps the
    // block when the request still fits and stays aligned, and otherwise allocates, copies the old
    // usable bytes up to the new size and frees. Exhaustion traps, where the CLR throws
    // OutOfMemoryException.
    internal static class NativeHeapWasm
    {
        private const int Heads = 0, Bump = 128, Limit = 132, MinimumClass = 5;

        // Indexes into the scratch array of six i32 Wasm locals.
        internal const int Size = 0, Alignment = 1, SizeClass = 2, Block = 3, Extra = 4, Result = 5, ScratchCount = 6;

        // [] -> [] with `result` = a fresh block for `size` (> 0) bytes aligned to `alignment` (>= 8).
        internal static int Allocate(byte[] code, int used, int heap, int[] s, bool zero)
        {
            used = Create(code, used, heap);
            // size + alignment + 8 must fit in 31 bits.
            used = Get(code, used, s[Size]);
            used = Const(code, used, 0x7ffefff0);
            code[used++] = 0x4b; // i32.gt_u
            used = TrapIf(code, used);
            // sizeClass = max(5, 32 - clz(size + alignment + 7)): the smallest power of two that
            // holds the 8 header bytes, the alignment padding and the payload
            used = Const(code, used, 32);
            used = Get(code, used, s[Size]);
            used = Get(code, used, s[Alignment]);
            code[used++] = 0x6a;
            used = Const(code, used, 7);
            code[used++] = 0x6a;
            code[used++] = 0x67; // i32.clz
            code[used++] = 0x6b; // i32.sub
            used = Set(code, used, s[SizeClass]);
            // select(MinimumClass, sizeClass, sizeClass < MinimumClass)
            used = Const(code, used, MinimumClass);
            used = Get(code, used, s[SizeClass]);
            used = Get(code, used, s[SizeClass]);
            used = Const(code, used, MinimumClass);
            code[used++] = 0x48; // i32.lt_s
            code[used++] = 0x1b; // select
            used = Set(code, used, s[SizeClass]);

            // block = heads[sizeClass]; if block != 0 pop it, else bump-allocate.
            used = Head(code, used, heap, s[SizeClass]);
            used = Load(code, used, 0);
            used = Tee(code, used, s[Block]);
            code[used++] = 0x04; code[used++] = 0x40; // if
            used = Head(code, used, heap, s[SizeClass]);
            used = Get(code, used, s[Block]);
            used = Load(code, used, 0);
            used = Store(code, used, 0);
            code[used++] = 0x05; // else
            used = Const(code, used, 1);
            used = Get(code, used, s[SizeClass]);
            code[used++] = 0x74; // i32.shl: the block size
            used = Set(code, used, s[Extra]);
            // if (limit - bump < size) take fresh pages
            used = Global(code, used, heap);
            used = Load(code, used, Limit);
            used = Global(code, used, heap);
            used = Load(code, used, Bump);
            code[used++] = 0x6b;
            used = Get(code, used, s[Extra]);
            code[used++] = 0x49; // i32.lt_u
            code[used++] = 0x04; code[used++] = 0x40;
            used = Get(code, used, s[Extra]);
            used = Const(code, used, 0xffff);
            code[used++] = 0x6a;
            used = Const(code, used, 16);
            code[used++] = 0x76; // i32.shr_u: pages
            used = Tee(code, used, s[Result]);
            code[used++] = 0x40; code[used++] = 0x00; // memory.grow 0
            used = Tee(code, used, s[Block]);
            used = Const(code, used, -1);
            code[used++] = 0x46;
            used = TrapIf(code, used);
            // bump = old << 16; limit = bump + (pages << 16)
            used = Global(code, used, heap);
            used = Get(code, used, s[Block]);
            used = Const(code, used, 16);
            code[used++] = 0x74;
            used = Store(code, used, Bump);
            used = Global(code, used, heap);
            used = Get(code, used, s[Block]);
            used = Get(code, used, s[Result]);
            code[used++] = 0x6a;
            used = Const(code, used, 16);
            code[used++] = 0x74;
            used = Store(code, used, Limit);
            code[used++] = 0x0b; // end grow
            used = Global(code, used, heap);
            used = Load(code, used, Bump);
            used = Set(code, used, s[Block]);
            used = Global(code, used, heap);
            used = Get(code, used, s[Block]);
            used = Get(code, used, s[Extra]);
            code[used++] = 0x6a;
            used = Store(code, used, Bump);
            code[used++] = 0x0b; // end if/else

            // result = (block + 8 + alignment - 1) & -alignment; header = [block, sizeClass]
            used = Get(code, used, s[Block]);
            used = Const(code, used, 7);
            code[used++] = 0x6a;
            used = Get(code, used, s[Alignment]);
            code[used++] = 0x6a;
            used = Const(code, used, 0);
            used = Get(code, used, s[Alignment]);
            code[used++] = 0x6b; // -alignment
            code[used++] = 0x71; // i32.and
            used = Set(code, used, s[Result]);
            used = Before(code, used, s[Result], 8);
            used = Get(code, used, s[Block]);
            used = Store(code, used, 0);
            used = Before(code, used, s[Result], 4);
            used = Get(code, used, s[SizeClass]);
            used = Store(code, used, 0);
            if (zero)
            {
                used = Get(code, used, s[Result]);
                used = Const(code, used, 0);
                used = Get(code, used, s[Size]);
                code[used++] = 0xfc; code[used++] = 11; code[used++] = 0; // memory.fill
            }
            return used;
        }

        // [] -> []: returns the block at `address` (0 is a no-op) to its class list.
        internal static int Release(byte[] code, int used, int heap, int address, int[] s)
        {
            used = Get(code, used, address);
            code[used++] = 0x04; code[used++] = 0x40;
            used = Get(code, used, address);
            used = Load(code, used, -8);
            used = Set(code, used, s[Block]);
            used = Get(code, used, address);
            used = Load(code, used, -4);
            used = Set(code, used, s[SizeClass]);
            used = Get(code, used, s[Block]);
            used = Head(code, used, heap, s[SizeClass]);
            used = Load(code, used, 0);
            used = Store(code, used, 0);
            used = Head(code, used, heap, s[SizeClass]);
            used = Get(code, used, s[Block]);
            used = Store(code, used, 0);
            code[used++] = 0x0b;
            return used;
        }

        // [] -> [] with `result` = `address` resized to `size` (> 0) bytes at `alignment` (>= 8).
        internal static int Resize(byte[] code, int used, int heap, int address, int[] s)
        {
            used = Get(code, used, address);
            code[used++] = 0x45;
            code[used++] = 0x04; code[used++] = 0x40; // if (address == 0) allocate
            used = Allocate(code, used, heap, s, false);
            code[used++] = 0x05;
            // extra = usable bytes = block + (1 << class) - address
            used = Usable(code, used, address, s[Extra]);
            // keep the block when size <= usable and address % alignment == 0
            used = Get(code, used, s[Size]);
            used = Get(code, used, s[Extra]);
            code[used++] = 0x4d; // i32.le_u
            used = Get(code, used, address);
            used = Get(code, used, s[Alignment]);
            used = Const(code, used, 1);
            code[used++] = 0x6b;
            code[used++] = 0x71;
            code[used++] = 0x45;
            code[used++] = 0x71; // both
            code[used++] = 0x04; code[used++] = 0x40;
            used = Get(code, used, address);
            used = Set(code, used, s[Result]);
            code[used++] = 0x05;
            used = Allocate(code, used, heap, s, false);
            // copy min(usable, size) bytes from the old block, recomputed from its intact header
            used = Usable(code, used, address, s[Extra]);
            used = Get(code, used, s[Result]);
            used = Get(code, used, address);
            used = Get(code, used, s[Extra]);
            used = Get(code, used, s[Size]);
            used = Get(code, used, s[Extra]);
            used = Get(code, used, s[Size]);
            code[used++] = 0x49; // i32.lt_u
            code[used++] = 0x1b; // select: min
            code[used++] = 0xfc; code[used++] = 10; code[used++] = 0; code[used++] = 0; // memory.copy
            used = Release(code, used, heap, address, s);
            code[used++] = 0x0b;
            code[used++] = 0x0b;
            return used;
        }

        private static int Usable(byte[] code, int used, int address, int target)
        {
            used = Get(code, used, address);
            used = Load(code, used, -8);
            used = Const(code, used, 1);
            used = Get(code, used, address);
            used = Load(code, used, -4);
            code[used++] = 0x74;
            code[used++] = 0x6a;
            used = Get(code, used, address);
            code[used++] = 0x6b;
            return Set(code, used, target);
        }

        // Creates the control block on first use.
        private static int Create(byte[] code, int used, int heap)
        {
            used = Global(code, used, heap);
            code[used++] = 0x45;
            code[used++] = 0x04; code[used++] = 0x40;
            used = Const(code, used, 1);
            code[used++] = 0x40; code[used++] = 0x00; // memory.grow 0
            // [old pages] -> heap = old << 16 (a failed grow returns -1, whose shift is negative)
            used = Const(code, used, 16);
            code[used++] = 0x74;
            code[used++] = 0x24; // global.set
            used = WasmBinary.Unsigned(code, used, heap);
            used = Global(code, used, heap);
            used = Const(code, used, 0);
            code[used++] = 0x48; // i32.lt_s: the grow failed
            used = TrapIf(code, used);
            used = Global(code, used, heap);
            used = Global(code, used, heap);
            used = Const(code, used, 256);
            code[used++] = 0x6a;
            used = Store(code, used, Bump);
            used = Global(code, used, heap);
            used = Global(code, used, heap);
            used = Const(code, used, 65536);
            code[used++] = 0x6a;
            used = Store(code, used, Limit);
            code[used++] = 0x0b;
            return used;
        }

        // [] -> [address of heads[sizeClass]]
        private static int Head(byte[] code, int used, int heap, int sizeClass)
        {
            used = Global(code, used, heap);
            used = Get(code, used, sizeClass);
            used = Const(code, used, 2);
            code[used++] = 0x74;
            code[used++] = 0x6a;
            return used;
        }

        private static int TrapIf(byte[] code, int used)
        {
            code[used++] = 0x04; code[used++] = 0x40; code[used++] = 0x00; code[used++] = 0x0b;
            return used;
        }

        // [address] -> [i32]: i32.load with alignment 4. A negative offset is applied to the address
        // first, because memarg offsets are unsigned.
        private static int Load(byte[] code, int used, int offset)
        {
            if (offset < 0) { used = Const(code, used, offset); code[used++] = 0x6a; offset = 0; }
            code[used++] = 0x28; code[used++] = 2;
            return WasmBinary.Unsigned(code, used, offset);
        }

        // [address, value] -> []; the offset is a nonnegative memarg offset.
        private static int Store(byte[] code, int used, int offset)
        {
            code[used++] = 0x36; code[used++] = 2;
            return WasmBinary.Unsigned(code, used, offset);
        }

        // [] -> [local - bytes]: a header address below a payload.
        private static int Before(byte[] code, int used, int local, int bytes)
        {
            used = Get(code, used, local);
            used = Const(code, used, bytes);
            code[used++] = 0x6b;
            return used;
        }

        private static int Global(byte[] code, int used, int heap)
        {
            code[used++] = 0x23;
            return WasmBinary.Unsigned(code, used, heap);
        }

        private static int Get(byte[] code, int used, int local) => Local(code, used, 0x20, local);
        private static int Set(byte[] code, int used, int local) => Local(code, used, 0x21, local);
        private static int Tee(byte[] code, int used, int local) => Local(code, used, 0x22, local);
    }
}
