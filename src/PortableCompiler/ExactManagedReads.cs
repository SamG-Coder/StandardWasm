using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchIl;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class ExactManagedReads
    {
        internal static void FoldExplicitFieldReads(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int caller, int context, int[] methodArguments, int[] objects, int[] globals, int[] records, int[] clauses)
        {
            int[] counts = null;
            for (int address = 0; address < records.Length; address += Width)
            {
                if (records[address] != FieldAddress) continue;
                int asRef = records[address + 2] * Width;
                if (!DirectSpec(records, asRef, clauses)) continue;
                int asView = records[asRef + 2] * Width;
                if (!DirectSpec(records, asView, clauses)) continue;
                int load = records[asView + 2] * Width;
                if (load >= records.Length || (records[load] != LoadObject && records[load] != LoadIndirect) ||
                    ValueOperands.Entry(records, load / Width, clauses)) continue;

                if (counts == null)
                {
                    counts = new int[45];
                    if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) return;
                }
                var spec = new int[MethodSpecifications.Width];
                if (MethodSpecifications.Read(image, heaps, layout, counts, records[asView + 1], spec) < 0 ||
                    spec[MethodSpecifications.Arity] != 2 ||
                    !IsPlatformAs(image, heaps, layout, counts, spec[MethodSpecifications.Target])) continue;
                var arguments = new int[2];
                if (MethodInstantiations.ReadArguments(bundle, image, layout, heaps, owner, context, objects,
                    methodArguments, spec, arguments) < 0 || !ObjectLayouts.UsesPackedExplicitValue(objects, arguments[0]) ||
                    !GenericScalar.IsScalar(arguments[1])) continue;
                int source = arguments[0], target = arguments[1], storage = GenericScalar.Storage(target);
                if (ExplicitValueLayouts.Width(storage) == 0 || !MatchesScalarLoad(bundle, image, layout, heaps,
                    owner, context, methodArguments, objects, records[load], records[load + 1], target, storage)) continue;

                if (MethodSpecifications.Read(image, heaps, layout, counts, records[asRef + 1], spec) < 0 ||
                    spec[MethodSpecifications.Arity] != 1 ||
                    !IsPlatformUnsafe(image, heaps, layout, counts, spec[MethodSpecifications.Target], false)) continue;
                var sourceArgument = new int[1];
                if (MethodInstantiations.ReadArguments(bundle, image, layout, heaps, owner, context, objects,
                    methodArguments, spec, sourceArgument) < 0 || sourceArgument[0] != source ||
                    FieldType(bundle, image, layout, heaps, owner, caller, context, methodArguments, objects, globals,
                        counts[4], records[address + 1], records[address + 3] != 0) != source ||
                    !HasFirstScalarView(objects, source, storage)) continue;

                // The two authenticated calls only reinterpret the same address.
                // The one load consumes it. Read the exact offset-zero field view
                // using the existing default materialization and overlay decoding.
                records[address] = GetField;
                records[asRef] = Nop;
                records[asView] = Nop;
                records[load] = BoundScopedFieldLoad;
                records[load + 1] = 0;
                records[load + 3] = FieldAccess.Encode(source, storage);
            }
        }

        private static bool DirectSpec(int[] records, int at, int[] clauses) => at < records.Length &&
            records[at] == MethodSpecCall && records[at + 3] == 0 && !ValueOperands.Entry(records, at / Width, clauses);

        private static bool MatchesScalarLoad(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int context, int[] methodArguments, int[] objects, int kind, int operand, int target, int storage)
        {
            if (kind == LoadIndirect) return ValueOperands.PrimitiveLoadMatches(storage, operand);
            int row = operand & 0xffffff, table = operand >> 24;
            if (table == 0x1b)
                return ManagedReferenceSignatures.TypeSpecIdentity(bundle, image, layout, heaps,
                    owner, row, context, objects, methodArguments) == target;
            if (table != 1 && table != 2) return false;
            int element = ArrayTypeRefs.PrimitiveElement(image, heaps, layout, row << 2 | (table == 1 ? 1 : 0));
            return element > 0 && GenericScalar.Key(element) == target;
        }

        private static int FieldType(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int caller, int context, int[] methodArguments, int[] objects, int[] globals,
            int fieldCount, int row, bool linked)
        {
            if (linked)
            {
                var field = new int[4];
                int callerType = ObjectLayouts.Owner(image, heaps, layout, caller, false);
                return AssemblyBundle.ResolveField(bundle, image, layout, heaps, owner, row, false, false,
                    objects, globals, field, context, callerType, methodArguments) == 0 ? FieldAccess.Storage(field[1]) : 0;
            }
            if (row < 1 || row > fieldCount) return 0;
            int table = AssemblyReferences.FieldTable(image, heaps, layout);
            if (table < 0) return 0;
            var definition = new int[3];
            AssemblyReferences.ReadField(image, layout, table, row, definition);
            int blob = definition[2];
            if ((definition[0] & 0x10) != 0 || blob < 1 || blob >= heaps[1]) return 0;
            var next = new int[1];
            int limit = heaps[0] + heaps[1];
            int size = CompressedUnsigned(image, heaps[0] + blob, limit, next);
            int start = next[0];
            if (size < 2 || size > limit - start || image[start] != 6) return 0;
            next[0]++;
            int type = ManagedReferenceSignatures.Read(bundle, image, layout, heaps, owner, next, start + size,
                context, objects, 0);
            return next[0] == start + size ? type : 0;
        }

        private static bool HasFirstScalarView(int[] objects, int source, int storage)
        {
            int first = ObjectLayouts.FirstRow(objects, source);
            int end = first + ObjectLayouts.DeclaredFieldCount(objects, source);
            for (int field = first; field < end; field++)
                if (ObjectLayouts.FieldType(objects, source, field) == storage &&
                    ObjectLayouts.FieldOrdinal(objects, source, field) == 0) return true;
            return false;
        }

        // A same-type Unsafe.As immediately followed by ldobj reads the frame
        // value itself. No reference escapes and no bytes are reinterpreted.
        internal static void FoldExactManagedReads(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int context, int[] methodArguments, int[] objects, int[] frame, int[] records, int[] clauses)
        {
            for (int address = 0; address < records.Length; address += Width)
            {
                if (records[address] != FrameAddress) continue;
                int type = frame[records[address + 1]];
                int call = records[address + 2] * Width;
                if (call >= records.Length) continue;
                if (type == Native)
                {
                    FoldPointerAccessor(image, heaps, layout, records, clauses, address, call);
                    continue;
                }
                if (!ObjectLayouts.IsValueType(objects, type) ||
                    records[call] != MethodSpecCall || records[call + 3] != 0) continue;
                int load = records[call + 2] * Width;
                if (load >= records.Length || records[load] != LoadObject ||
                    ValueOperands.Entry(records, call / Width, clauses) || ValueOperands.Entry(records, load / Width, clauses)) continue;

                var counts = new int[45];
                if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) continue;
                var spec = new int[MethodSpecifications.Width];
                if (MethodSpecifications.Read(image, heaps, layout, counts, records[call + 1], spec) < 0 ||
                    spec[MethodSpecifications.Arity] != 2 ||
                    !IsPlatformAs(image, heaps, layout, counts, spec[MethodSpecifications.Target])) continue;
                var arguments = new int[2];
                if (MethodInstantiations.ReadArguments(bundle, image, layout, heaps, owner,
                    context, objects, methodArguments, spec, arguments) < 0 || arguments[0] != type || arguments[1] != type) continue;

                int token = records[load + 1], table = token >> 24;
                if (table != 1 && table != 2 && table != 0x1b) continue;
                int coded = (token & 0xffffff) << 2 | (table == 1 ? 1 : table == 2 ? 0 : 2);
                var localIdentity = new int[2];
                var loadIdentity = new int[2];
                if (RuntimeTypeTokens.ReadLayoutIdentity(objects, type, localIdentity) < 0 ||
                    RuntimeTypeTokens.ReadIdentity(bundle, image, layout, heaps, owner, coded,
                        context, objects, methodArguments, loadIdentity) < 0 ||
                    localIdentity[0] != loadIdentity[0] || localIdentity[1] != loadIdentity[1]) continue;

                records[address] = Load;
                records[call] = Nop;
                records[load] = Nop;
            }
        }

        private static void FoldPointerAccessor(byte[] image, int[] heaps, int[] layout, int[] records,
            int[] clauses, int address, int call)
        {
            int kind = records[call];
            bool member = kind == MemberCall;
            if ((!member && kind != Call) || records[call + 3] != (member ? 0x28 : 0) ||
                ValueOperands.Entry(records, call / Width, clauses) ||
                !NativePointerMetadata.IsPointerAccessor(image, heaps, layout, records[call + 1], member)) return;

            // The accessor only reads the pointer bits already in the slot.
            // Do this before planning native storage for any other address uses.
            records[address] = Load;
            records[call] = Nop;
        }

        internal static bool IsPlatformAs(byte[] image, int[] heaps, int[] layout, int[] counts, int target) =>
            IsPlatformUnsafe(image, heaps, layout, counts, target, true);

        private static bool IsPlatformUnsafe(byte[] image, int[] heaps, int[] layout, int[] counts, int target, bool reinterprets)
        {
            int row = target >> 1;
            int signature, name;
            if ((target & 1) == 0)
            {
                int owner = ObjectLayouts.Owner(image, heaps, layout, row, false);
                if (owner < 1 || (MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x10) == 0 || !SpanMetadata.IsPlatformType(image, layout, heaps, owner << 2,
                    "System.Runtime.CompilerServices", "Unsafe")) return false;
                signature = MethodDefinitions.ReadColumn(image, layout, row, 4);
                name = MethodDefinitions.ReadColumn(image, layout, row, 3);
            }
            else
            {
                var member = new int[AssemblyReferences.SymbolWidth];
                if (AssemblyReferences.ReadMember(image, heaps, layout, row, member) < 0 ||
                    member[AssemblyReferences.Depth] != 0 || !MatchesAscii(image, heaps, (uint)member[2], "Unsafe") ||
                    !MatchesAscii(image, heaps, (uint)member[3], "System.Runtime.CompilerServices") ||
                    !PlatformAssemblyIdentity.IsUnsafeAssembly(image, heaps, layout, counts, member[AssemblyReferences.AssemblyRow]))
                    return false;
                signature = member[5];
                name = member[4];
            }
            if (signature < 1 || signature >= heaps[1] ||
                !MatchesAscii(image, heaps, (uint)name, reinterprets ? "As" : "AsRef")) return false;
            int limit = heaps[0] + heaps[1];
            var next = new int[1];
            int size = CompressedUnsigned(image, heaps[0] + signature, limit, next);
            int start = next[0];
            if (size != 9 || size > limit - start || image[start + 8] != 0) return false;
            // AsRef takes the managed byref overload, distinct from the void* overload.
            return reinterprets ? U32(image, (uint)start) == 0x10010210 &&
                U32(image, (uint)(start + 4)) == 0x1e10011e : U32(image, (uint)start) == 0x10010110 &&
                U32(image, (uint)(start + 4)) == 0x1e10001e;
        }
    }
}
