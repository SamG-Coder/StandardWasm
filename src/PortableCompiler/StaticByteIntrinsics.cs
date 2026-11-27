using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    internal static class StaticByteIntrinsics
    {
        internal static bool IsLittleEndian(byte[] image, int[] layout, int[] heaps, int row)
        {
            int table = AssemblyReferences.FieldTable(image, heaps, layout);
            if (table < 0) return false;
            var field = new int[3];
            AssemblyReferences.ReadField(image, layout, table, row, field);
            if ((field[0] & 0x30) != 0x30 || !MatchesAscii(image, heaps, (uint)field[1], "IsLittleEndian") ||
                !Fits((uint)field[2], 3, (uint)heaps[1])) return false;
            int owner = ObjectLayouts.Owner(image, heaps, layout, row, true);
            if (owner < 1 || !SpanMetadata.IsPlatformType(image, layout, heaps, owner << 2, "System", "BitConverter"))
                return false;
            uint signature = (uint)(heaps[0] + field[2]);
            return U16(image, signature) == 0x0602 && image[signature + 2] == 2;
        }

        internal static int Identify(byte[] image, int[] layout, int[] heaps, int[] counts, int[] spec,
            int[] methodArguments)
        {
            int argument = spec[MethodSpecifications.Arguments];
            int end = spec[MethodSpecifications.End];
            if (spec[MethodSpecifications.Arity] != 1 || argument >= end) return 0;
            int key = image[argument];
            if (key == 0x1e && methodArguments != null)
            {
                var afterIndex = new int[1];
                int index = CompressedUnsigned(image, argument + 1, end, afterIndex);
                if (index < 0 || index >= methodArguments.Length || afterIndex[0] != end ||
                    methodArguments[index] != GenericScalar.Key(3)) return 0;
                key = 3;
            }
            else if (end != argument + 1) return 0;
            if (key != 3 && key != 5 && key != 7) return 0; // Char, Byte or UInt16.

            int target = spec[MethodSpecifications.Target], row = target >> 1;
            int signature, name;
            bool marshal, unsafeType;
            if ((target & 1) == 0)
            {
                if (!PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts) ||
                    (MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x10) == 0) return 0;
                int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
                int owner = ObjectLayouts.Owner(image, heaps, layout, row, false);
                if (first < 0 || owner < 1) return 0;
                marshal = ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, owner,
                    "System.Runtime.InteropServices", "MemoryMarshal");
                unsafeType = ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, owner,
                    "System.Runtime.CompilerServices", "Unsafe");
                signature = MethodDefinitions.ReadColumn(image, layout, row, 4);
                name = MethodDefinitions.ReadColumn(image, layout, row, 3);
            }
            else
            {
                var member = new int[AssemblyReferences.SymbolWidth];
                if (AssemblyReferences.ReadMember(image, heaps, layout, row, member) < 0) return 0;
                marshal = SpanMetadata.MatchReference(image, layout, heaps, counts, member,
                    "System.Runtime.InteropServices", "MemoryMarshal", includeMemory: true);
                unsafeType = SpanMetadata.MatchReference(image, layout, heaps, counts, member,
                    "System.Runtime.CompilerServices", "Unsafe");
                signature = member[5];
                name = member[4];
            }

            if ((!marshal && !unsafeType) || signature < 1 || signature >= heaps[1]) return 0;
            var next = new int[1];
            int limit = heaps[0] + heaps[1];
            int size = CompressedUnsigned(image, heaps[0] + signature, limit, next);
            int start = next[0];
            if (size <= 0 || size > limit - start) return 0;
            if (marshal && (key == 3 || key == 5) && MatchesAscii(image, heaps, (uint)name, "GetReference") &&
                IsSpanReference(image, layout, heaps, start, start + size, next, writable: key == 3))
                return key == 3 ? TakeCharacterSpanLocation : TakeStaticByteLocation;

            if (!unsafeType) return 0;
            // static T& AddByteOffset<T>(T&, nint/nuint)
            if ((key == 3 || key == 5) && size == 10 && MatchesAscii(image, heaps, (uint)name, "AddByteOffset") &&
                U32(image, (uint)start) == 0x10020110 && U32(image, (uint)(start + 4)) == 0x1e10001e &&
                image[start + 8] == 0 && (image[start + 9] == 0x18 || image[start + 9] == 0x19))
                return key == 3 ? OffsetCharacterLocation : OffsetStaticByteLocation;
            // static T ReadUnaligned<T>(byte&)
            if (key == 7 && size == 7 && MatchesAscii(image, heaps, (uint)name, "ReadUnaligned") &&
                U32(image, (uint)start) == 0x1e010110 && image[start + 4] == 0 &&
                U16(image, (uint)(start + 5)) == 0x0510)
                return ReadStaticUInt16;
            return 0;
        }

        // static T& GetReference<T>(valuetype Span<T> or ReadOnlySpan<T>).
        private static bool IsSpanReference(byte[] image, int[] layout, int[] heaps, int start, int end,
            int[] next, bool writable)
        {
            if (end - start < 12 || U32(image, (uint)start) != 0x10010110 ||
                U32(image, (uint)(start + 4)) != 0x1115001e) return false;
            int span = CompressedUnsigned(image, start + 8, end, next);
            int at = next[0];
            return span > 0 && end - at == 3 && image[at] == 1 && image[at + 1] == 0x1e &&
                image[at + 2] == 0 && SpanMetadata.IsDefinition(image, layout, heaps, span, writable);
        }
    }
}
