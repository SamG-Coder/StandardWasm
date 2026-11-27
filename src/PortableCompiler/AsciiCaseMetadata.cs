using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class AsciiCaseMetadata
    {
        private const byte Token = 0xff;

        internal static bool IsOriginalLowercase(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int row, int[] objects)
        {
            if (!MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), "ToLowerAsciiInvariant"))
                return false;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts) ||
                !CoreStringRange.IsMethod(image, layout, heaps, counts, first, row, "System.Globalization", "TextInfo",
                    "ToLowerAsciiInvariant", new byte[] { 0, 1, 0x0e, 0x0e }, true)) return false;
            var body = new int[6];
            if (!ReadBody(image, layout, row, 0xb3, body) || body[3] == 0) return false;
            int at = body[0];
            if (!Operations(image, at, body[1], new byte[]
            {
                // Length/empty check and first uppercase ASCII search.
                0x02, 0x6f, Token, 0x2d, 0x06, 0x7e, Token, 0x2a, 0x02, 0x28, Token, 0x1f,
                0x41, 0x1f, 0x5a, 0x28, Token, 0x0a, 0x06, 0x16, 0x2f, 0x02, 0x02, 0x2a,
                // Pin the input and allocate and pin the fresh output.
                0x02, 0x2d, 0x04, 0x16, 0xe0, 0x2b, 0x09, 0x02, 0x28, Token, 0x0c, 0x08,
                0xe0, 0x0b, 0x02, 0x6f, Token, 0x28, Token, 0x0d, 0x09, 0x2d, 0x04, 0x16,
                0xe0, 0x2b, 0x0b, 0x09, 0x28, Token, 0x13, 0x05, 0x11, 0x05, 0xe0, 0x13,
                // Copy the unchanged prefix; the first uppercase code unit is known.
                0x04, 0x02, 0x16, 0x06, 0x28, Token, 0x13, 0x06, 0x12, 0x06, 0x11, 0x04,
                0x09, 0x6f, Token, 0x73, Token, 0x28, Token, 0x11, 0x04, 0x06, 0xd3, 0x18,
                0x5a, 0x58, 0x07, 0x06, 0xd3, 0x18, 0x5a, 0x58, 0x49, 0x1f, 0x20, 0x60,
                // Lowercase remaining ASCII units and release the pins.
                0xd1, 0x53, 0x06, 0x17, 0x58, 0x0a, 0x2b, 0x18, 0x11, 0x04, 0x06, 0xd3,
                0x18, 0x5a, 0x58, 0x07, 0x06, 0xd3, 0x18, 0x5a, 0x58, 0x49, 0x28, Token,
                0x53, 0x06, 0x17, 0x58, 0x0a, 0x06, 0x02, 0x6f, Token, 0x32, 0xdf, 0x16,
                0xe0, 0x13, 0x05, 0x09, 0x2a,
            })) return false;
            int length = ReadToken(image, at + 2, 6);
            int empty = ReadToken(image, at + 9, 4);
            int pin = ReadToken(image, at + 0x2d, 6);
            return length > 0 && StringMetadata.AccessorDefinition(image, heaps, layout, length) == StringMetadata.Length &&
                ReadToken(image, at + 0x37, 6) == length && ReadToken(image, at + 0x65, 6) == length &&
                ReadToken(image, at + 0xa7, 6) == length &&
                empty > 0 && StringMetadata.IsEmptyDefinition(image, heaps, layout, empty) &&
                SpanMetadata.StringAsSpanArity(image, layout, heaps, ReadToken(image, at + 0x10, 6), false) == 1 &&
                SpanMetadata.StringAsSpanArity(image, layout, heaps, ReadToken(image, at + 0x59, 6), false) == 3 &&
                IsSearch(image, layout, heaps, counts, first, (uint)(at + 0x19)) &&
                pin > 0 && ReadToken(image, at + 0x4a, 6) == pin && IsStringPin(image, layout, heaps, counts, first, pin) &&
                CoreStringRange.IsMethod(image, layout, heaps, counts, first, ReadToken(image, at + 0x3c, 6),
                    "System", "String", "FastAllocateString", new byte[] { 0, 1, 0x0e, 8 }, true) &&
                IsDestinationSpan(bundle, image, layout, heaps, owner, ReadToken(image, at + 0x6a, 10), objects) &&
                SpanMetadata.IsCharacterCopyTo(bundle, image, layout, heaps, owner, ReadToken(image, at + 0x6f, 10),
                    0, null, CharacterSpan, objects) &&
                IsScalarLowercase(image, layout, heaps, counts, first, ReadToken(image, at + 0x9b, 6));
        }

        private static bool IsSearch(byte[] image, int[] layout, int[] heaps, int[] counts, int first, uint tokenAt)
        {
            int row = ReadToken(image, (int)tokenAt, 0x2b);
            var spec = new int[MethodSpecifications.Width];
            if (row <= 0 || MethodSpecifications.Read(image, heaps, layout, counts, row, spec) < 0) return false;
            int argument = spec[MethodSpecifications.Arguments];
            int target = spec[MethodSpecifications.Target];
            if (spec[MethodSpecifications.Arity] != 1 || spec[MethodSpecifications.End] != argument + 1 ||
                image[argument] != 3 || target <= 0 || (target & 1) != 0) return false;
            int method = target >> 1;
            int type = ObjectLayouts.Owner(image, heaps, layout, method, false);
            if (!ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, type, "System", "MemoryExtensions") ||
                !MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, method, 3), "IndexOfAnyInRange")) return false;
            int blob = MethodDefinitions.ReadColumn(image, layout, method, 4);
            if (blob <= 0 || blob >= heaps[1]) return false;
            var cursor = new int[1];
            int limit = heaps[0] + heaps[1];
            int size = CompressedUnsigned(image, heaps[0] + blob, limit, cursor);
            if (size < 14 || size > limit - cursor[0]) return false;
            int end = cursor[0] + size;
            if (!SignatureReader.TakeElement(image, cursor, end, 0x10) ||
                !SignatureReader.TakeElement(image, cursor, end, 1) ||
                !SignatureReader.TakeElement(image, cursor, end, 3) ||
                !SignatureReader.TakeElement(image, cursor, end, 8)) return false;
            int span = SignatureReader.ReadSingleArgumentValue(image, cursor, end);
            if (span <= 0 || !SpanMetadata.IsDefinition(image, layout, heaps, span)) return false;
            for (int i = 0; i < 3; i++)
                if (!SignatureReader.TakeElement(image, cursor, end, 0x1e) ||
                    !SignatureReader.TakeElement(image, cursor, end, 0)) return false;
            return cursor[0] == end;
        }

        private static bool IsStringPin(byte[] image, int[] layout, int[] heaps, int[] counts, int first, int row)
        {
            int owner = ObjectLayouts.Owner(image, heaps, layout, row, false);
            var body = new int[6];
            if (!ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, owner, "System", "String") ||
                !MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), "GetPinnableReference") ||
                !ReadBody(image, layout, row, 7, body) || body[3] != 0 ||
                !Operations(image, body[0], body[1], new byte[] { 0x02, 0x7c, Token, 0x2a })) return false;
            int field = ReadToken(image, body[0] + 2, 4);
            return field > 0 && StringMetadata.IsFirstCodeUnitDefinition(image, heaps, layout, field);
        }

        private static bool IsDestinationSpan(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int member, int[] objects)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (member <= 0 || AssemblyReferences.ReadMember(image, heaps, layout, member, reference, allowClosedParent: true) < 0 ||
                !MatchesAscii(image, heaps, (uint)reference[4], ".ctor")) return false;
            int parent = reference[AssemblyReferences.GenericParent], blob = reference[5];
            if (parent == 0 || blob <= 0 || !Fits((uint)blob, 7, (uint)heaps[1])) return false;
            int at = heaps[0] + blob;
            return U32(image, (uint)at) == 0x01022006 && image[at + 4] == 0x0f && image[at + 5] == 1 &&
                image[at + 6] == 8 && ManagedReferenceSignatures.ReadInstantiation(bundle, image, layout, heaps, owner,
                    parent, objects) == WritableCharacterSpan;
        }

        private static bool IsScalarLowercase(byte[] image, int[] layout, int[] heaps, int[] counts, int first, int row)
        {
            var body = new int[6];
            return CoreStringRange.IsMethod(image, layout, heaps, counts, first, row, "System.Globalization", "TextInfo",
                "ToLowerAsciiInvariant", new byte[] { 0, 1, 3, 3 }, true) &&
                ReadBody(image, layout, row, 17, body) && body[3] == 0 &&
                Operations(image, body[0], body[1], new byte[]
                {
                    0x02, 0x28, Token, 0x2c, 7, 0x02, 0x1f, 0x20, 0x60, 0xd2, 0x10, 0, 0x02, 0x2a,
                }) &&
                CoreStringRange.IsMethod(image, layout, heaps, counts, first, ReadToken(image, body[0] + 2, 6),
                    "System", "Char", "IsAsciiLetterUpper", new byte[] { 0, 1, 2, 3 }, true);
        }

        private static bool ReadBody(byte[] image, int[] layout, int row, int length, int[] body) =>
            MethodBodies.Read(image, (uint)MethodDefinitions.ReadColumn(image, layout, row, 0), body) == 0 &&
            body[1] == length && body[5] == 0;

        private static int ReadToken(byte[] image, int at, int table)
        {
            uint value = U32(image, (uint)at);
            return value >> 24 == table ? (int)(value & 0xffffff) : -1;
        }

        private static bool Operations(byte[] image, int at, int length, byte[] expected)
        {
            int end = at + length;
            for (int i = 0; i < expected.Length; i++)
            {
                byte op = expected[i];
                if (op == Token)
                {
                    if (end - at < 4) return false;
                    at += 4;
                }
                else if (at >= end || image[at++] != op) return false;
            }
            return at == end;
        }
    }
}
