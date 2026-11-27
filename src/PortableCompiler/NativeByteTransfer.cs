using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchIl;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // Linear memory and managed byte arrays have different storage. Fuse the
    // verified readonly use of a native byte span into the actual destination
    // array, after the upstream Marshal guards have executed.
    internal sealed class NativeByteTransfer
    {
        internal const int Tail = 0x2c * Width;
        internal const int CallSlot = 0x30 * Width;
        private const int ReturnSlot = 0x43 * Width;
        internal int Function { get; private set; } = -1;

        internal static bool IsCopy(byte[] image, int[] layout, int[] heaps,
            int caller, int[] typeArguments, int[] records, int at, int[] clauses)
        {
            if (at != Tail || records.Length != ReturnSlot + Width || clauses.Length != 0 ||
                typeArguments == null || typeArguments.Length != 1 || typeArguments[0] != GenericScalar.Key(5)) return false;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts) ||
                !CoreStringRange.IsMethod(image, layout, heaps, counts, first, caller, "System.Runtime.InteropServices",
                    "Marshal", "CopyToManaged", new byte[] { 0x10, 1, 4, 1, 0x18, 0x1d, 0x1e, 0, 8, 8 }, true)) return false;
            var body = new int[6];
            uint rva = (uint)MethodDefinitions.ReadColumn(image, layout, caller, 0);
            if (MethodBodies.Read(image, rva, body) != 0 || body[1] != 0x44 || body[2] < 4 || body[5] != 0) return false;
            for (int pc = 0x2c; pc <= 0x43; pc++)
                if (ValueOperands.Entry(records, pc, clauses)) return false;

            int start = body[0];
            for (int guard = 0; guard < 4; guard++)
            {
                int offset = start + guard * 11;
                int input = guard == 0 ? 3 : guard == 1 ? 2 : guard + 2;
                if (image[offset] != input || image[offset + 1] != 0x72 || image[offset + 6] != 0x28) return false;
            }
            if (!CoreStringRange.IsMethod(image, layout, heaps, counts, first, Token(image, start + 7, 6),
                    "System", "ArgumentNullException", "ThrowIfNull", new byte[] { 0, 2, 1, 0x1c, 0x0e }, true) ||
                !CoreStringRange.IsMethod(image, layout, heaps, counts, first, Token(image, start + 0x12, 6),
                    "System", "ArgumentNullException", "ThrowIfNull", new byte[] { 0, 2, 1, 0x18, 0x0e }, true) ||
                U32(image, (uint)start + 0x1d) != U32(image, (uint)start + 0x28) ||
                !CoreStringRange.IsClosedMethod(image, layout, heaps, counts, first, U32(image, (uint)start + 0x1d),
                    "System", "ArgumentOutOfRangeException", "ThrowIfNegative",
                    new byte[] { 8 }, new byte[] { 0x10, 1, 2, 1, 0x1e, 0, 0x0e })) return false;

            // Span<byte>(source, count); Span<byte>(destination, start, count);
            // one CopyTo consuming both, with no reuse or escaping native view.
            return U16(image, (uint)start + 0x2c) == 0x0502 && image[start + 0x2e] == 0x73 &&
                U32(image, (uint)start + 0x33) == 0x0300120a && U16(image, (uint)start + 0x37) == 0x0504 &&
                image[start + 0x39] == 0x73 && image[start + 0x3e] == 0x28 && image[start + 0x43] == 0x2a &&
                Member(image, layout, heaps, Token(image, start + 0x2f, 10), 0) &&
                Member(image, layout, heaps, Token(image, start + 0x3a, 10), 1) &&
                Member(image, layout, heaps, Token(image, start + 0x3f, 10), 2);
        }

        private static bool Member(byte[] image, int[] layout, int[] heaps,
            int row, int operation)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (row <= 0 || AssemblyReferences.ReadMember(image, heaps, layout, row, reference, allowClosedParent: true) < 0 ||
                !MatchesAscii(image, heaps, (uint)reference[4], operation == 2 ? "CopyTo" : ".ctor")) return false;
            int parent = reference[AssemblyReferences.GenericParent];
            var cursor = new int[1];
            int limit = heaps[0] + heaps[1];
            if (parent <= 0 || parent >= heaps[1]) return false;
            int size = CompressedUnsigned(image, heaps[0] + parent, limit, cursor);
            if (size < 6 || size > limit - cursor[0]) return false;
            int parentEnd = cursor[0] + size;
            int spanType = SignatureReader.ReadSingleArgumentValue(image, cursor, parentEnd);
            if (spanType <= 0 || !SpanMetadata.IsDefinition(image, layout, heaps, spanType, writable: true) ||
                !SignatureReader.TakeElement(image, cursor, parentEnd, 0x1e) ||
                !SignatureReader.TakeElement(image, cursor, parentEnd, 0) || cursor[0] != parentEnd) return false;
            int end = SignatureReader.ReadInstance(image, heaps, reference[5], operation == 0 ? 2 : operation == 1 ? 3 : 1, cursor);
            if (end < 0 || !SignatureReader.TakeElement(image, cursor, end, SignatureReader.Void)) return false;
            if (operation == 0)
                return SignatureReader.TakeElement(image, cursor, end, 0x0f) &&
                    SignatureReader.TakeElement(image, cursor, end, SignatureReader.Void) &&
                    SignatureReader.TakeElement(image, cursor, end, 8) && cursor[0] == end;
            if (operation == 1)
                return SignatureReader.TakeElement(image, cursor, end, 0x1d) && IsElementParameter(image, cursor, end) &&
                    SignatureReader.TakeElement(image, cursor, end, 8) && SignatureReader.TakeElement(image, cursor, end, 8) &&
                    cursor[0] == end;
            int span = SignatureReader.ReadSingleArgumentValue(image, cursor, end);
            return span > 0 && SpanMetadata.IsDefinition(image, layout, heaps, span, writable: true) &&
                IsElementParameter(image, cursor, end) && cursor[0] == end;
        }

        private static bool IsElementParameter(byte[] image, int[] cursor, int end) =>
            SignatureReader.TakeElement(image, cursor, end, 0x13) && SignatureReader.TakeElement(image, cursor, end, 0);

        private static int Token(byte[] image, int at, int table)
        {
            uint value = U32(image, (uint)at);
            return value >> 24 == table ? (int)(value & 0xffffff) : -1;
        }

        internal int Bind(int[] arities, int[] offsets, int[] signatures, int functions)
        {
            if (Function >= 0) return functions;
            int offset = MethodCompiler.NextSignature(offsets, arities, functions);
            if (functions >= arities.Length || signatures.Length - offset < 5) return -3;
            Function = functions;
            offsets[functions] = offset;
            arities[functions] = 4;
            signatures[offset] = Void;
            signatures[offset + 1] = Native;
            signatures[offset + 2] = Bytes;
            signatures[offset + 3] = I32;
            signatures[offset + 4] = I32;
            return functions + 1;
        }

        internal void Rewrite(int[] records)
        {
            for (int at = Tail; at < ReturnSlot; at++) records[at] = 0;
            for (int argument = 0; argument < 4; argument++)
            {
                int at = Tail + argument * Width;
                records[at] = Load;
                records[at + 1] = argument;
                records[at + 2] = at / Width + 1;
            }
            records[CallSlot] = Call;
            records[CallSlot + 1] = Function;
            records[CallSlot + 2] = ReturnSlot / Width;
        }
    }
}
