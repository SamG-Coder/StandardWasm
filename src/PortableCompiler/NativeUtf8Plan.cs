using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchIl;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // Keep Marshal's original zero check and native length discovery. Replace
    // the authenticated singleton Encoding.UTF8 consumer with the Wasm GC
    // string constructor, which cannot expose a pinned managed char pointer.
    internal sealed class NativeUtf8Plan
    {
        internal int Decoder { get; private set; } = -1;
        internal int Measure { get; private set; } = -1;
        internal int Empty { get; private set; }

        internal static bool IsDecodePair(byte[] image, int[] layout, int[] heaps, int row, int[] records, int at, int[] clauses)
        {
            const int getter = 0x13 * Width, decoder = 0x18 * Width;
            if (at != getter || records.Length != 0x1e * Width || clauses.Length != 0 ||
                records[at] != Call || records[at + 3] != 0 || records[at + 2] != 0x18 ||
                records[decoder] != Call || records[decoder + 3] != 0 || records[decoder + 2] != 0x1d ||
                records[0x1d * Width] != Return || ValueOperands.Entry(records, 0x18, clauses)) return false;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts) ||
                !CoreStringRange.IsMethod(image, layout, heaps, counts, first, row, "System.Runtime.InteropServices",
                    "Marshal", "PtrToStringUTF8", new byte[] { 0, 1, 0x0e, 0x18 }, true)) return false;
            int utf8 = records[at + 1];
            return Method(image, layout, heaps, counts, first, utf8, "System.Text", "Encoding", "get_UTF8", 0) &&
                IsDefaultSingleton(image, layout, heaps, counts, first, utf8) &&
                Method(image, layout, heaps, counts, first, records[decoder + 1], "System", "String", "CreateStringFromEncoding", 3);
        }

        private static bool IsDefaultSingleton(byte[] image, int[] layout, int[] heaps, int[] counts, int first, int getter)
        {
            var body = new int[6];
            int rva = MethodDefinitions.ReadColumn(image, layout, getter, 0);
            if (MethodBodies.Read(image, (uint)rva, body) != 0 || body[1] != 6 || body[3] != 0 || body[5] != 0) return false;
            int at = body[0];
            uint token = U32(image, (uint)at + 1);
            if (image[at] != 0x7e || image[at + 5] != 0x2a || token >> 24 != 4) return false;
            int row = (int)(token & 0xffffff);
            int owner = ObjectLayouts.Owner(image, heaps, layout, row, true);
            if (owner < 1 || !ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, owner, "System.Text", "UTF8Encoding"))
                return false;
            int table = AssemblyReferences.FieldTable(image, heaps, layout);
            if (table < 0) return false;
            var field = new int[3];
            AssemblyReferences.ReadField(image, layout, table, row, field);
            if ((field[0] & 0x30) != 0x30 || !MatchesAscii(image, heaps, (uint)field[1], "s_default")) return false;

            // The canonical initializer creates its sealed UTF-8 singleton.
            // The Boolean argument selects the preamble, not the fallback.
            int initializer = TypeInitializers.Find(image, layout, heaps, owner, new int[1]);
            if (initializer <= 0 || MethodBodies.Read(image,
                (uint)MethodDefinitions.ReadColumn(image, layout, initializer, 0), body) != 0 ||
                body[1] != 12 || body[3] != 0 || body[5] != 0) return false;
            at = body[0];
            uint constructor = U32(image, (uint)at + 2);
            int ctor = (int)(constructor & 0xffffff);
            if (image[at] != 0x17 || image[at + 1] != 0x73 || image[at + 6] != 0x80 ||
                U32(image, (uint)at + 7) != token || image[at + 11] != 0x2a || constructor >> 24 != 6 ||
                !ObjectLayouts.IsConstructor(image, heaps, layout, ctor)) return false;
            int sealedType = ObjectLayouts.Owner(image, heaps, layout, ctor, false);
            if (NestedTypes.Enclosing(image, heaps, layout, counts, sealedType) != owner ||
                !ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, sealedType, "", "UTF8EncodingSealed", nested: true))
                return false;
            var cursor = new int[1];
            int end = SignatureReader.ReadInstance(image, heaps, MethodDefinitions.ReadColumn(image, layout, ctor, 4), 1, cursor);
            return end >= 0 && SignatureReader.TakeElement(image, cursor, end, SignatureReader.Void) &&
                SignatureReader.TakeElement(image, cursor, end, SignatureReader.Boolean) && cursor[0] == end;
        }
        internal static bool IsLengthCall(byte[] image, int[] layout, int[] heaps, int row, int[] records, int at, int[] clauses)
        {
            if (at != Width || records.Length != 7 * Width || clauses.Length != 0 ||
                records[0] != Load || records[1] != 0 || records[2] != 1 ||
                records[at] != Call || records[at + 3] != 0 || records[at + 2] != 6 ||
                records[6 * Width] != Return || records[6 * Width + 2] != 7) return false;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            var signature = new byte[] { 0, 1, 8, 0x0f, 5 };
            return first >= 0 && PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts) &&
                CoreStringRange.IsMethod(image, layout, heaps, counts, first, row, "System", "String", "strlen", signature, true) &&
                CoreStringRange.IsMethod(image, layout, heaps, counts, first, records[at + 1], "System", "SpanHelpers",
                    "IndexOfNullByte", signature, true);
        }

        internal int BindLength(int[] arities, int[] offsets, int[] signatures, int functions)
        {
            if (Measure >= 0) return functions;
            int offset = MethodCompiler.NextSignature(offsets, arities, functions);
            if (functions >= arities.Length || signatures.Length - offset < 2) return -3;
            Measure = functions;
            offsets[functions] = offset;
            arities[functions] = 1;
            signatures[offset] = I32;
            signatures[offset + 1] = Native;
            return functions + 1;
        }

        private static bool Method(byte[] image, int[] layout, int[] heaps, int[] counts, int first, int row,
            string space, string type, string name, int arity)
        {
            if (row < 1 || row > counts[6] ||
                (MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x10) == 0 ||
                !MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), name)) return false;
            int owner = ObjectLayouts.Owner(image, heaps, layout, row, false);
            if (owner < 1 || !ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, owner, space, type))
                return false;
            var cursor = new int[1];
            int end = SignatureReader.ReadStatic(image, heaps, MethodDefinitions.ReadColumn(image, layout, row, 4), arity, cursor);
            if (end < 0) return false;
            if (arity == 3 && (!SignatureReader.TakeElement(image, cursor, end, 0x0e) ||
                !SignatureReader.TakeElement(image, cursor, end, 0x0f) ||
                !SignatureReader.TakeElement(image, cursor, end, 5) ||
                !SignatureReader.TakeElement(image, cursor, end, 8))) return false;
            int encoding = SignatureReader.ReadClass(image, cursor, end);
            return encoding > 0 && (encoding & 3) == 0 && cursor[0] == end &&
                ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, encoding >> 2, "System.Text", "Encoding");
        }

        internal int Bind(int[] globals, int[] arities, int[] offsets, int[] signatures, int functions)
        {
            if (Decoder >= 0) return functions;
            int offset = MethodCompiler.NextSignature(offsets, arities, functions);
            if (functions >= arities.Length || signatures.Length - offset < 3) return -3;
            int empty = StringLiterals.Empty(globals);
            if (empty < 0) return empty;
            Empty = empty;
            Decoder = functions;
            offsets[functions] = offset;
            arities[functions] = 2;
            signatures[offset] = String;
            signatures[offset + 1] = Native;
            signatures[offset + 2] = I32;
            return functions + 1;
        }
    }
}
