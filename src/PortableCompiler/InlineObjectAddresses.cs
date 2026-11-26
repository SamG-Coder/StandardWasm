using PeLibrary;
using static PeLibrary.ImageBytes;
using static PeLibrary.ObjectLayouts;
using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    internal static class InlineObjectAddresses
    {
        // Roslyn uses these helpers for an in-range constant element and a
        // fixed-length span. Validate the helper and its closed types first.
        internal static int Bind(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int context, int[] callerArguments, int[] records, int slot,
            int[] frame, int[] locations, int[] objects, int[] clauses)
        {
            int local = records[slot + 1], type = frame[local];
            if (!IsValueType(objects, type) || !IsInlineArray(objects, type) ||
                FieldTypeAt(objects, type, 0) != ObjectReference) return 0;
            int index = records[slot + 2] * Width;
            if (index >= records.Length || records[index] != Constant ||
                ValueOperands.Entry(records, index / Width, clauses)) return 0;
            int call = records[index + 2] * Width;
            if (call >= records.Length || records[call] != MethodSpecCall || records[call + 3] != 0 ||
                ValueOperands.Entry(records, call / Width, clauses)) return 0;

            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0) return -1;
            var spec = new int[MethodSpecifications.Width];
            int status = MethodSpecifications.Read(image, heaps, layout, counts, records[call + 1], spec);
            if (status < 0) return status;
            int target = spec[MethodSpecifications.Target];
            if ((target & 1) != 0 || spec[MethodSpecifications.Arity] != 2) return 0;
            int kind = GeneratedHelper(image, layout, heaps, counts, first, target >> 1);
            if (kind == 0) return 0;
            uint limit = (uint)(FieldCount(objects, type) - (kind == ObjectElementAddress ? 1 : 0));
            if ((uint)records[index + 1] > limit) return 0;
            var arguments = new int[2];
            status = MethodInstantiations.ReadArguments(bundle, image, layout, heaps, owner, context,
                objects, callerArguments, spec, arguments);
            if (status < 0) return status;
            if (arguments[0] != type || arguments[1] != ObjectReference) return 0;

            int vector = RegisterArray(objects, ObjectReference);
            if (vector < 0) return vector;
            locations[local] = vector;
            records[slot] = LoadInlineStorage;
            records[slot + 3] = vector;
            records[call] = kind;
            records[call + 1] = vector;
            records[call + 3] = BoundObjectElement;
            return 1;
        }

        private static int GeneratedHelper(byte[] image, int[] layout, int[] heaps,
            int[] counts, int first, int row)
        {
            if ((MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x10) == 0) return 0;
            uint name = (uint)MethodDefinitions.ReadColumn(image, layout, row, 3);
            int signature = MethodDefinitions.ReadColumn(image, layout, row, 4);
            bool span = MatchesAscii(image, heaps, name, "InlineArrayAsReadOnlySpan");
            if (span)
            {
                if (!ReadOnlySpanMetadata.MatchesFactorySignature(image, layout, heaps, signature, 2, 1, 0))
                    return 0;
            }
            else if (!MatchesAscii(image, heaps, name, "InlineArrayElementRef") ||
                !Signature(image, heaps, signature, new byte[] { 0x10, 2, 2, 0x10, 0x1e, 1, 0x10, 0x1e, 0, 8 }))
                return 0;

            int owner = Owner(image, heaps, layout, row, false);
            if (!NamedDefinition(image, heaps, layout, counts, first, owner, "", "<PrivateImplementationDetails>"))
                return 0;
            var body = new int[6];
            uint rva = unchecked((uint)MethodDefinitions.ReadColumn(image, layout, row, 0));
            if (MethodBodies.Read(image, rva, body) < 0 || body[1] != (span ? 18 : 13) ||
                body[3] != 0 || body[5] != 0) return 0;
            int start = body[0];
            if (image[start] != 2 || image[start + body[1] - 1] != 0x2a) return 0;

            // ldarg.0; [AsRef<TBuffer>]; As<TBuffer,TElement>; ldarg.1;
            // Add<TElement> or CreateReadOnlySpan<TElement>; ret.
            if (span)
            {
                int asRef = PlatformSpec(image, layout, heaps, counts, first, start + 1,
                    "System.Runtime.CompilerServices", "Unsafe", "AsRef", 0);
                if (!Signature(image, heaps, asRef, new byte[] { 0x10, 1, 1, 0x10, 0x1e, 0, 0x10, 0x1e, 0 }))
                    return 0;
                start += 5;
            }
            if (image[start + 6] != 3) return 0;
            int asElement = PlatformSpec(image, layout, heaps, counts, first, start + 1,
                "System.Runtime.CompilerServices", "Unsafe", "As", 0, 1);
            if (!Signature(image, heaps, asElement, new byte[] { 0x10, 2, 1, 0x10, 0x1e, 1, 0x10, 0x1e, 0 }))
                return 0;

            if (span)
            {
                int create = PlatformSpec(image, layout, heaps, counts, first, start + 7,
                    "System.Runtime.InteropServices", "MemoryMarshal", "CreateReadOnlySpan", 1);
                return ReadOnlySpanMetadata.MatchesFactorySignature(image, layout, heaps, create, 1, 0, 0)
                    ? NewObjectSpan : 0;
            }
            int add = PlatformSpec(image, layout, heaps, counts, first, start + 7,
                "System.Runtime.CompilerServices", "Unsafe", "Add", 1);
            return Signature(image, heaps, add, new byte[] { 0x10, 1, 2, 0x10, 0x1e, 0, 0x10, 0x1e, 0, 8 })
                ? ObjectElementAddress : 0;
        }

        private static int PlatformSpec(byte[] image, int[] layout, int[] heaps, int[] counts, int first,
            int at, string space, string type, string method, int firstArgument, int secondArgument = -1)
        {
            if (image[at] != 0x28) return 0;
            uint token = U32(image, (uint)(at + 1));
            if (token >> 24 != 0x2b) return 0;
            var spec = new int[MethodSpecifications.Width];
            if (MethodSpecifications.Read(image, heaps, layout, counts, (int)(token & 0xffffff), spec) < 0)
                return 0;

            int start = spec[MethodSpecifications.Arguments];
            int arguments = secondArgument < 0 ? 1 : 2;
            if (spec[MethodSpecifications.Arity] != arguments || spec[MethodSpecifications.End] - start != arguments * 2 ||
                image[start] != 0x1e || image[start + 1] != firstArgument ||
                arguments == 2 && (image[start + 2] != 0x1e || image[start + 3] != secondArgument))
                return 0;

            int target = spec[MethodSpecifications.Target], row = target >> 1;
            int name, signature;
            if ((target & 1) == 0)
            {
                if (!PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts)) return 0;
                int owner = Owner(image, heaps, layout, row, false);
                if (!NamedDefinition(image, heaps, layout, counts, first, owner, space, type)) return 0;
                name = MethodDefinitions.ReadColumn(image, layout, row, 3);
                signature = MethodDefinitions.ReadColumn(image, layout, row, 4);
            }
            else
            {
                var reference = new int[AssemblyReferences.SymbolWidth];
                if (AssemblyReferences.ReadMember(image, heaps, layout, row, reference) < 0 ||
                    !ReadOnlySpanMetadata.MatchReference(image, layout, heaps, counts, reference, space, type)) return 0;
                name = reference[4];
                signature = reference[5];
            }
            return MatchesAscii(image, heaps, (uint)name, method) ? signature : 0;
        }

        private static bool Signature(byte[] image, int[] heaps, int blob, byte[] expected)
        {
            if (blob < 1 || blob >= heaps[1]) return false;
            var next = new int[1];
            int limit = heaps[0] + heaps[1];
            int length = CompressedUnsigned(image, heaps[0] + blob, limit, next);
            if (length != expected.Length || length > limit - next[0]) return false;
            for (int i = 0; i < length; i++)
                if (image[next[0] + i] != expected[i]) return false;
            return true;
        }
    }
}
