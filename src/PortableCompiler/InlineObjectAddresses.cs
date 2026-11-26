using PeLibrary;
using static PeLibrary.ImageBytes;
using static PeLibrary.ObjectLayouts;
using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    internal static class InlineObjectAddresses
    {
        // Roslyn uses this helper for a statically in-range element. Check the
        // closed types and the helper body before replacing the two Unsafe calls.
        internal static int Bind(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int context, int[] callerArguments, int[] records, int slot,
            int[] frame, int[] locations, int[] objects, int[] clauses)
        {
            int local = records[slot + 1], type = frame[local];
            if (!IsValueType(objects, type) || !IsInlineArray(objects, type) ||
                FieldTypeAt(objects, type, 0) != ObjectReference) return 0;
            int index = records[slot + 2] * Width;
            if (index >= records.Length || records[index] != Constant ||
                (uint)records[index + 1] >= (uint)FieldCount(objects, type) ||
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
            if ((target & 1) != 0 || spec[MethodSpecifications.Arity] != 2 ||
                !GeneratedElementBody(image, layout, heaps, counts, first, target >> 1)) return 0;
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
            records[call] = ObjectElementAddress;
            records[call + 1] = vector;
            records[call + 3] = BoundObjectElement;
            return 1;
        }

        private static bool GeneratedElementBody(byte[] image, int[] layout, int[] heaps,
            int[] counts, int first, int row)
        {
            if ((MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x10) == 0 ||
                !MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), "InlineArrayElementRef") ||
                !Signature(image, heaps, MethodDefinitions.ReadColumn(image, layout, row, 4),
                    new byte[] { 0x10, 2, 2, 0x10, 0x1e, 1, 0x10, 0x1e, 0, 8 })) return false;
            int owner = Owner(image, heaps, layout, row, false);
            if (!NamedDefinition(image, heaps, layout, counts, first, owner, "", "<PrivateImplementationDetails>"))
                return false;

            var body = new int[6];
            uint rva = unchecked((uint)MethodDefinitions.ReadColumn(image, layout, row, 0));
            if (MethodBodies.Read(image, rva, body) < 0 || body[1] != 13 || body[3] != 0 || body[5] != 0)
                return false;
            int start = body[0];
            // ldarg.0; call Unsafe.As<!!0, !!1>; ldarg.1; call Unsafe.Add<!!1>; ret
            if (image[start] != 2 || image[start + 1] != 0x28 || image[start + 6] != 3 ||
                image[start + 7] != 0x28 || image[start + 12] != 0x2a) return false;
            return UnsafeSpec(image, layout, heaps, counts, first, U32(image, (uint)(start + 2)), true) &&
                UnsafeSpec(image, layout, heaps, counts, first, U32(image, (uint)(start + 8)), false);
        }

        private static bool UnsafeSpec(byte[] image, int[] layout, int[] heaps, int[] counts,
            int first, uint token, bool reinterpret)
        {
            if (token >> 24 != 0x2b) return false;
            var spec = new int[MethodSpecifications.Width];
            if (MethodSpecifications.Read(image, heaps, layout, counts, (int)(token & 0xffffff), spec) < 0)
                return false;
            int start = spec[MethodSpecifications.Arguments];
            int length = spec[MethodSpecifications.End] - start;
            if (reinterpret ? spec[MethodSpecifications.Arity] != 2 || length != 4 ||
                U32(image, (uint)start) != 0x011e001e : spec[MethodSpecifications.Arity] != 1 ||
                length != 2 || U16(image, (uint)start) != 0x011e) return false;

            const string scope = "System.Runtime.CompilerServices";
            int target = spec[MethodSpecifications.Target], row = target >> 1;
            int name, signature;
            if ((target & 1) == 0)
            {
                if (!PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts)) return false;
                int owner = Owner(image, heaps, layout, row, false);
                if (!NamedDefinition(image, heaps, layout, counts, first, owner, scope, "Unsafe")) return false;
                name = MethodDefinitions.ReadColumn(image, layout, row, 3);
                signature = MethodDefinitions.ReadColumn(image, layout, row, 4);
            }
            else
            {
                var reference = new int[AssemblyReferences.SymbolWidth];
                if (AssemblyReferences.ReadMember(image, heaps, layout, row, reference) < 0 ||
                    reference[AssemblyReferences.Depth] != 0 ||
                    !MatchesAscii(image, heaps, (uint)reference[2], "Unsafe") ||
                    !MatchesAscii(image, heaps, (uint)reference[3], scope) ||
                    !PlatformAssemblyIdentity.IsPlatformAssembly(image, heaps, layout, counts,
                        reference[AssemblyReferences.AssemblyRow], includeRuntimeExtensions: false)) return false;
                name = reference[4];
                signature = reference[5];
            }
            return MatchesAscii(image, heaps, (uint)name, reinterpret ? "As" : "Add") &&
                Signature(image, heaps, signature, reinterpret
                    ? new byte[] { 0x10, 2, 1, 0x10, 0x1e, 1, 0x10, 0x1e, 0 }
                    : new byte[] { 0x10, 1, 2, 0x10, 0x1e, 0, 0x10, 0x1e, 0, 8 });
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
