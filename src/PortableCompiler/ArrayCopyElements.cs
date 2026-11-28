using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    // Decision 0105: byte[], bool[] and sbyte[] share the Bytes vector (char[], short[] and
    // ushort[] share Packed16), and Array.Copy between two of them is an
    // ArrayTypeMismatchException on the CLR, so ArrayCopyWasm rejects a packed vector it cannot
    // tell apart. This pass proves the CLI element type of both arrays from the straight-line
    // instructions that produce them (ldfld/ldsfld of a field, or newarr) and, when they are the
    // same primitive, lowers the call to ArrayCopy with the Exact flag.
    internal static class ArrayCopyElements
    {
        internal const int Exact = 2;

        internal static void Lower(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int[] records, int[] clauses)
        {
            for (int call = 0; call < records.Length; call += Width)
            {
                if (records[call] != MemberCall || records[call + 3] != 0x28) continue;
                int arity = ArrayCopyMetadata.Arity(image, layout, heaps, records[call + 1], true);
                if (arity == 0) continue;
                var producers = new int[arity];
                if (!Producers(records, clauses, call, producers)) continue;
                int source = Element(image, layout, heaps, records, producers[0]);
                int destination = Element(image, layout, heaps, records, producers[arity == 3 ? 1 : 2]);
                if (source == 0 || source != destination) continue;
                records[call] = ArrayCopy;
                records[call + 1] = arity;
                records[call + 3] = (arity == 5 && ArrayCopyMetadata.ChecksIndicesBeforeRanges(bundle) ? 1 : 0) | Exact;
            }
        }

        // The record that produced each argument of `call`, found by simulating the straight-line
        // records before it from the nearest start that yields exactly the arguments.
        private static bool Producers(int[] records, int[] clauses, int call, int[] producers)
        {
            int start = call;
            for (int step = 0; step < 32; step++)
            {
                start = Previous(records, clauses, start);
                if (start < 0) return false;
                int result = Simulate(records, start, call, producers);
                if (result == producers.Length) return true;
                if (result == Unknown) return false;
            }
            return false;
        }

        private const int Unknown = -1000;

        // The number of values the records from `start` up to `call` leave, filling `producers`;
        // -1 on underflow; Unknown for a record outside the simple set.
        private static int Simulate(int[] records, int start, int call, int[] producers)
        {
            var stack = new int[64];
            int depth = 0;
            for (int at = start; at != call; at = records[at + 2] * Width)
            {
                int kind = records[at];
                int pops = kind == GetField || kind == ArrayLength || kind == ConvertInteger || kind == Unary ? 1 :
                    kind == Binary ? 2 : 0;
                bool pushes = kind == Load || kind == Constant || kind == WideConstant || kind == LoadStaticField ||
                    kind == NewArray || pops != 0 || kind == Dup;
                if (kind == NewArray) pops = 1;
                if (!pushes && kind != Nop) return Unknown;
                if (kind == Dup) pops = 1;
                if (depth < pops) return -1;
                int copied = kind == Dup ? stack[depth - 1] : at;
                depth -= pops;
                if (kind == Dup) stack[depth++] = copied;
                if (pushes)
                {
                    if (depth >= stack.Length) return Unknown;
                    stack[depth++] = copied;
                }
            }
            if (depth != producers.Length) return depth > producers.Length ? Unknown : depth;
            for (int i = 0; i < depth; i++) producers[i] = stack[i];
            return depth;
        }

        // The unique straight-line predecessor of `slot` that is not a branch target, or -1.
        private static int Previous(int[] records, int[] clauses, int slot)
        {
            if (ValueOperands.Entry(records, slot / Width, clauses)) return -1;
            int found = -1;
            for (int at = 0; at < records.Length; at += Width)
                if (records[at] != 0 && records[at] != Branch && records[at] != Return && records[at + 2] * Width == slot)
                {
                    if (found >= 0) return -1;
                    found = at;
                }
            return found;
        }

        // The packed primitive element (bool, char, sbyte, byte, short, ushort) of the SZARRAY a
        // producer pushes, or 0 when it is unknown or not packed.
        private static int Element(byte[] image, int[] layout, int[] heaps, int[] records, int producer)
        {
            int kind = records[producer], element = 0;
            if (kind == GetField || kind == LoadStaticField)
            {
                int blob;
                if (records[producer + 3] == 0)
                {
                    int table = AssemblyReferences.FieldTable(image, heaps, layout);
                    if (table < 0) return 0;
                    var field = new int[3];
                    AssemblyReferences.ReadField(image, layout, table, records[producer + 1], field);
                    blob = field[2];
                }
                else
                {
                    var reference = new int[AssemblyReferences.SymbolWidth];
                    if (AssemblyReferences.ReadMember(image, heaps, layout, records[producer + 1], reference, allowClosedParent: true) < 0)
                        return 0;
                    blob = reference[5];
                }
                if (blob < 1 || !Fits((uint)blob, 4, (uint)heaps[1])) return 0;
                uint s = (uint)(heaps[0] + blob);
                if (image[s] != 3 || image[s + 1] != 0x06 || image[s + 2] != 0x1d) return 0;
                element = image[s + 3];
            }
            else if (kind == NewArray)
            {
                int token = records[producer + 1];
                int table = (int)((uint)token >> 24), row = token & 0xffffff;
                if (table != 1 && table != 2) return 0;
                element = ArrayTypeRefs.PrimitiveElement(image, heaps, layout, row << 2 | (table == 1 ? 1 : 0));
            }
            return element >= 0x02 && element <= 0x07 ? element : 0;
        }
    }
}
