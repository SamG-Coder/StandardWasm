using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchIl;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // Decision 0104: System.Runtime.CompilerServices.Unsafe calls over primitive type arguments.
    //
    // - SkipInit<T>(out T): the CLR leaves the target's bits unspecified; Wasm locals and cells
    //   already hold a valid value. On `ldloca x` both records become nops; any other address
    //   (such as an out parameter) is popped.
    // - BitCast<TFrom, TTo>(TFrom) for primitives of equal size: the bits are reinterpreted.
    //   Integer stack values are normalized to the target (a byte or Boolean target keeps the low
    //   8 bits, sbyte and short sign-extend, char and ushort keep 16 bits); float and integer pairs
    //   use the Wasm reinterpret instructions.
    internal static class UnsafeCalls
    {
        internal static void Lower(byte[] image, int[] layout, int[] heaps, int[] records, int[] clauses)
        {
            int[] counts = null;
            for (int slot = 0; slot < records.Length; slot += Width)
            {
                if (records[slot] != MethodSpecCall || records[slot + 3] != 0) continue;
                if (counts == null)
                {
                    counts = new int[45];
                    if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) return;
                }
                var spec = new int[MethodSpecifications.Width];
                if (MethodSpecifications.Read(image, heaps, layout, counts, records[slot + 1], spec) < 0) continue;
                int target = spec[MethodSpecifications.Target];
                if ((target & 1) == 0) continue; // a MemberRef into the platform Unsafe class
                var member = new int[AssemblyReferences.SymbolWidth];
                if (AssemblyReferences.ReadMember(image, heaps, layout, target >> 1, member) < 0 ||
                    member[AssemblyReferences.Depth] != 0 || !MatchesAscii(image, heaps, (uint)member[2], "Unsafe") ||
                    !MatchesAscii(image, heaps, (uint)member[3], "System.Runtime.CompilerServices") ||
                    !PlatformAssemblyIdentity.IsUnsafeAssembly(image, heaps, layout, counts, member[AssemblyReferences.AssemblyRow]))
                    continue;
                uint name = (uint)member[4];
                int arity = spec[MethodSpecifications.Arity], arguments = spec[MethodSpecifications.Arguments];
                if (arity == 1 && MatchesAscii(image, heaps, name, "SkipInit"))
                {
                    int address = Producer(records, slot, clauses);
                    if (address >= 0 && records[address] == FrameAddress)
                    {
                        records[address] = Nop;
                        records[slot] = Nop;
                    }
                    else records[slot] = Pop; // an existing managed address (an out parameter): [address] -> []
                    continue;
                }
                if (arity != 2 || !MatchesAscii(image, heaps, name, "BitCast") ||
                    arguments + 2 != spec[MethodSpecifications.End]) continue;
                int from = image[arguments], to = image[arguments + 1];
                if (Size(from) == 0 || Size(from) != Size(to)) continue;
                records[slot] = PrimitiveBitCast;
                records[slot + 1] = from;
                records[slot + 3] = to;
            }
        }

        // The record whose only successor is `slot`, when `slot` is not a branch target.
        private static int Producer(int[] records, int slot, int[] clauses)
        {
            if (ValueOperands.Entry(records, slot / Width, clauses)) return -1;
            for (int at = 0; at < records.Length; at += Width)
                if (records[at] != 0 && records[at + 2] * Width == slot) return at;
            return -1;
        }

        // Byte size of a primitive element type, or 0.
        internal static int Size(int element) =>
            element == 0x02 || element == 0x04 || element == 0x05 ? 1 :
            element == 0x03 || element == 0x06 || element == 0x07 ? 2 :
            element == 0x08 || element == 0x09 || element == 0x0c ? 4 :
            element == 0x0a || element == 0x0b || element == 0x0d ? 8 : 0;

        // Evaluation-stack type of a primitive element type.
        internal static int StackType(int element) =>
            element == 0x0c ? F32 : element == 0x0d ? F64 : Size(element) == 8 ? I64 : I32;

        // [from] -> [to]
        internal static int Emit(byte[] code, int used, int from, int to)
        {
            int source = StackType(from), target = StackType(to);
            if (source != target)
            {
                code[used++] = (byte)CoreScalarBitCast.Opcode(source, target);
                return used;
            }
            if (to == 0x02 || to == 0x05)
            {
                used = BranchWasm.Const(code, used, 0xff);
                code[used++] = 0x71; // i32.and
            }
            else if (to == 0x04) code[used++] = 0xc0; // i32.extend8_s
            else if (to == 0x03 || to == 0x07)
            {
                used = BranchWasm.Const(code, used, 0xffff);
                code[used++] = 0x71;
            }
            else if (to == 0x06) code[used++] = 0xc1; // i32.extend16_s
            return used;
        }
    }
}
