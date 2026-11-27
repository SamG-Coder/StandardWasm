using PeLibrary;
using static PeLibrary.ImageBytes;

namespace PortableCompiler
{
    internal static class StaticInterfaceSlots
    {
        // A closed constrained call has no receiver on the evaluation stack.
        // Resolve the declaration first and then the concrete type's MethodImpl.
        internal static int Resolve(BundleImages bundle, byte[] caller, int[] layout, int[] heaps,
            int owner, int typeToken, int memberToken, int context, int[] methodArguments, int[] objects, int[] result)
        {
            if (bundle.Length == 0 || (uint)typeToken >> 24 != 0x1b) return -2;
            int blob = AssemblyReferences.TypeSpecBlob(caller, heaps, layout, typeToken & 0xffffff);
            if (blob < 0) return blob;
            var cursor = new int[1];
            int heapEnd = heaps[0] + heaps[1];
            int size = CompressedUnsigned(caller, heaps[0] + blob, heapEnd, cursor);
            if (size < 1 || size > heapEnd - cursor[0]) return -1;
            int end = cursor[0] + size;
            int constraint = ManagedReferenceSignatures.Read(bundle, caller, layout, heaps, owner, cursor,
                end, context, objects, 0, methodArguments: methodArguments);
            if (constraint < 0) return constraint;
            if (cursor[0] != end) return -1;

            int slot = memberToken & 0xffffff;
            int[] contract, reference = null;
            if ((uint)memberToken >> 24 == 6)
            {
                // A local MethodDef already identifies the nominal slot and has
                // no generic parent substitution. Its declaring type must be an interface.
                int type = ObjectLayouts.Owner(caller, heaps, layout, slot, false);
                if (type < 1) return -1;
                if (!IsStaticDeclaration(caller, layout, slot, true)) return -2;
                var shape = new int[5];
                if (ObjectLayouts.ReadShape(caller, heaps, layout, type, shape) < 0) return -1;
                if ((shape[4] & 0x20) == 0) return -2;
                contract = new[] { owner, type };
            }
            else
            {
                reference = new int[AssemblyReferences.SymbolWidth];
                int resolved = AssemblyReferences.ReadMember(caller, heaps, layout, slot, reference,
                    allowClosedParent: true, allowLocalParent: true);
                if (resolved < 0) return resolved;
                contract = BundleConstraints.ReadInterface(bundle, caller, layout, heaps, owner, objects,
                    reference[AssemblyReferences.ParentType], null, context, methodArguments);
            }
            if (contract[0] < 0) return contract[0];
            int status = BundleConstraints.Implements(bundle, objects, constraint, contract, 0);
            if (status <= 0) return status < 0 ? status : -2;

            int declaredOwner = contract[0];
            byte[] declared = bundle.Get(declaredOwner);
            var declaredLayout = new int[4];
            var declaredHeaps = new int[8];
            if (CompressedTables.FindWithHeaps(declared, declaredLayout, declaredHeaps) < 0) return -1;
            if (reference != null)
            {
                slot = FindDeclaration(bundle, caller, layout, heaps, owner, reference, contract[1],
                    declared, declaredLayout, declaredHeaps, declaredOwner);
                if (slot <= 0) return slot < 0 ? slot : -2;
            }

            var definition = new int[2];
            int targetContext = 0;
            if (GenericScalar.IsScalar(constraint))
            {
                status = BundleConstraints.FindPrimitive(bundle, constraint, definition);
                if (status <= 0) return status < 0 ? status : -2;
            }
            else
            {
                definition[0] = ObjectLayouts.AssemblyOf(objects, constraint);
                definition[1] = ObjectLayouts.TypeDef(objects, constraint);
                if (definition[0] < 0 || definition[1] < 1) return -2;
                targetContext = constraint;
            }
            byte[] target = bundle.Get(definition[0]);
            var targetLayout = new int[4];
            var targetHeaps = new int[8];
            if (CompressedTables.FindWithHeaps(target, targetLayout, targetHeaps) < 0) return -1;
            int body = FindImplementation(bundle, objects, contract, declared, declaredLayout, declaredHeaps,
                slot, target, targetLayout, targetHeaps, definition[0], definition[1], targetContext);
            if (body <= 0) return body < 0 ? body : -2;
            result[0] = definition[0];
            result[1] = body;
            result[2] = ObjectLayouts.HasArguments(objects, targetContext) ? targetContext : 0;
            return 0;
        }

        private static int FindDeclaration(BundleImages bundle, byte[] caller, int[] callerLayout, int[] callerHeaps,
            int callerOwner, int[] reference, int type, byte[] declared, int[] layout, int[] heaps, int owner)
        {
            var shape = new int[5];
            int status = ObjectLayouts.ReadShape(declared, heaps, layout, type, shape);
            if (status < 0) return status;
            if ((shape[4] & 0x20) == 0) return -2;
            var range = new int[2];
            status = VirtualSlotMetadata.ReadMethodRange(declared, heaps, layout, type, range);
            if (status < 0) return status;
            int slot = 0;
            for (int row = range[0]; row < range[1]; row++)
            {
                if (!IsStaticDeclaration(declared, layout, row, callerOwner == owner)) continue;
                int name = MethodDefinitions.ReadColumn(declared, layout, row, 3);
                if (!AssemblyReferences.SameString(caller, callerHeaps, reference[4], declared, heaps, name)) continue;
                int signature = MethodDefinitions.ReadColumn(declared, layout, row, 4);
                status = GenericMethodSignatures.Match(bundle, caller, callerLayout, callerHeaps, callerOwner, reference[5],
                    declared, layout, heaps, owner, signature, 0);
                if (status < 0) return status;
                if (status == 0) continue;
                if (slot != 0) return -5;
                slot = row;
            }
            return slot;
        }

        private static bool IsStaticDeclaration(byte[] image, int[] layout, int row, bool sameAssembly)
        {
            int flags = MethodDefinitions.ReadColumn(image, layout, row, 2), access = flags & 7;
            return (flags & 0x450) == 0x450 && (access == 6 || sameAssembly && access == 3);
        }

        private static int FindImplementation(BundleImages bundle, int[] objects, int[] contract,
            byte[] declared, int[] declaredLayout, int[] declaredHeaps, int slot,
            byte[] image, int[] layout, int[] heaps, int owner, int type, int context)
        {
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) return -1;
            if (counts[25] == 0) return 0;
            int table = MetadataTableRows.TableStart(heaps, layout, counts, 25);
            if (table < 0) return -1;
            int classWidth = counts[2] < 65536 ? 2 : 4;
            int methodWidth = MetadataTableRows.MethodRefWidth(counts);
            int width = classWidth + 2 * methodWidth;
            int name = MethodDefinitions.ReadColumn(declared, declaredLayout, slot, 3);
            int signature = MethodDefinitions.ReadColumn(declared, declaredLayout, slot, 4);
            int selected = 0;
            var reference = new int[AssemblyReferences.SymbolWidth];
            for (int i = 0; i < counts[25]; i++)
            {
                uint at = (uint)(table + i * width);
                if (Index(image, at, classWidth) != type) continue;
                int token = Index(image, at + (uint)(classWidth + methodWidth), methodWidth);
                if ((token & 1) == 0)
                {
                    if (contract.Length != 2 || owner != contract[0] || token >> 1 != slot) continue;
                }
                else
                {
                    int status = AssemblyReferences.ReadMember(image, heaps, layout, token >> 1, reference, allowClosedParent: true, allowLocalParent: true);
                    if (status == -2) continue;
                    if (status < 0) return status;
                    if (!AssemblyReferences.SameString(image, heaps, reference[4], declared, declaredHeaps, name)) continue;
                    int[] candidate = BundleConstraints.ReadInterface(bundle, image, layout, heaps, owner, objects,
                        reference[AssemblyReferences.ParentType], null, context);
                    if (!BundleConstraints.Same(candidate, contract)) continue;
                    status = GenericMethodSignatures.Match(bundle, image, layout, heaps, owner, reference[5],
                        declared, declaredLayout, declaredHeaps, contract[0], signature, 0);
                    if (status < 0) return status;
                    if (status == 0) continue;
                }
                int body = Index(image, at + (uint)classWidth, methodWidth);
                if ((body & 1) != 0 || body >> 1 < 1 || body >> 1 > counts[6]) return -2;
                int row = body >> 1;
                if (ObjectLayouts.Owner(image, heaps, layout, row, false) != type) return -2;
                if ((MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x450) != 0x10) return -2;
                int bodySignature = MethodDefinitions.ReadColumn(image, layout, row, 4);
                int match = MatchBody(bundle, objects, contract, declared, declaredLayout, declaredHeaps, signature,
                    image, layout, heaps, owner, bodySignature, context);
                if (match <= 0) return match < 0 ? match : -2;
                if (selected != 0) return -5;
                selected = row;
            }
            return selected;
        }

        private static int MatchBody(BundleImages bundle, int[] objects, int[] contract,
            byte[] declared, int[] declaredLayout, int[] declaredHeaps, int signature,
            byte[] image, int[] layout, int[] heaps, int owner, int bodySignature, int context)
        {
            var left = new int[1];
            var right = new int[1];
            int leftEnd = GenericMethodSignatures.Start(declared, declaredHeaps, signature, left, 0);
            int rightEnd = GenericMethodSignatures.Start(image, heaps, bodySignature, right, 0);
            if (leftEnd <= 0 || rightEnd <= 0) return 0;
            int count = CompressedUnsigned(declared, left[0], leftEnd, left);
            if (count < 0 || count != CompressedUnsigned(image, right[0], rightEnd, right)) return 0;
            var declaringArguments = new int[contract.Length - 2];
            for (int i = 0; i < declaringArguments.Length; i++) declaringArguments[i] = contract[i + 2];
            for (int i = 0; i <= count; i++)
            {
                if (left[0] >= leftEnd || right[0] >= rightEnd) return 0;
                if (i == 0)
                {
                    int returnKind = ReadReturnKind(bundle, declared, declaredLayout, declaredHeaps, contract[0], left, leftEnd);
                    if (returnKind < 0) return returnKind;
                    int bodyKind = ReadReturnKind(bundle, image, layout, heaps, owner, right, rightEnd);
                    if (bodyKind < 0 || returnKind != bodyKind) return bodyKind < 0 ? bodyKind : 0;
                    if (left[0] >= leftEnd || right[0] >= rightEnd) return 0;
                }
                if (i == 0 && declared[left[0]] == 1 && image[right[0]] == 1)
                {
                    left[0] = left[0] + 1;
                    right[0] = right[0] + 1;
                    continue;
                }
                int expected = ManagedReferenceSignatures.Read(bundle, declared, declaredLayout, declaredHeaps, contract[0],
                    left, leftEnd, 0, objects, 0, declaringArguments);
                if (expected < 0) return expected;
                int actual = ManagedReferenceSignatures.Read(bundle, image, layout, heaps, owner, right, rightEnd, context, objects, 0);
                if (actual != expected) return 0;
            }
            return left[0] == leftEnd && right[0] == rightEnd ? 1 : 0;
        }

        private static int ReadReturnKind(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int[] cursor, int end)
        {
            bool readOnly = image[cursor[0]] == 0x1f;
            if (readOnly && !ReadOnlyReferenceReturns.ReadModifier(bundle, image, layout, heaps, owner, cursor, end)) return -2;
            if (!SignatureReader.TakeElement(image, cursor, end, 0x10)) return 0;
            return readOnly ? 2 : 1;
        }

        private static int Index(byte[] image, uint at, int width) =>
            width == 2 ? (int)U16(image, at) : (int)U32(image, at);
    }
}
