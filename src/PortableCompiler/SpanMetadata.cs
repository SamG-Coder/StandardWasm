using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class SpanMetadata
    {
        internal static bool IsDefinition(byte[] image, int[] layout, int[] heaps, int coded, bool writable = false)
        {
            return IsPlatformType(image, layout, heaps, coded, "System", writable ? "Span`1" : "ReadOnlySpan`1");
        }

        internal static bool IsPlatformType(byte[] image, int[] layout, int[] heaps,
            int coded, string space, string name)
        {
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0) return false;
            int row = coded >> 2;
            if ((coded & 3) == 0)
                return PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts) &&
                    ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, row, space, name);
            if ((coded & 3) != 1) return false;
            var reference = new int[AssemblyReferences.SymbolWidth];
            return AssemblyReferences.ReadType(image, heaps, layout, row, reference) >= 0 &&
                MatchReference(image, layout, heaps, counts, reference, space, name);
        }

        internal static bool MatchReference(byte[] image, int[] layout, int[] heaps, int[] counts,
            int[] reference, string space, string name, bool includeMemory = false)
        {
            return reference[AssemblyReferences.Depth] == 0 &&
                MatchesAscii(image, heaps, (uint)reference[2], name) &&
                MatchesAscii(image, heaps, (uint)reference[3], space) &&
                PlatformAssemblyIdentity.IsPlatformAssembly(image, heaps, layout, counts,
                    reference[AssemblyReferences.AssemblyRow], includeRuntimeExtensions: false, includeMemory: includeMemory);
        }

        // TBuffer, TElement or just TElement. The returned span must be the
        // platform generic definition, not a similarly named application type.
        internal static bool MatchesFactorySignature(byte[] image, int[] layout, int[] heaps,
            int blob, int arity, int resultArgument, int inputArgument)
        {
            if (blob < 1 || blob >= heaps[1]) return false;
            var next = new int[1];
            int limit = heaps[0] + heaps[1];
            int length = CompressedUnsigned(image, heaps[0] + blob, limit, next);
            int start = next[0];
            if (length < 13 || length > limit - start || image[start] != 0x10 ||
                image[start + 1] != arity || image[start + 2] != 2 ||
                image[start + 3] != 0x15 || image[start + 4] != 0x11) return false;

            int end = start + length;
            int span = CompressedUnsigned(image, start + 5, end, next);
            int at = next[0];
            return span > 0 && end - at == 7 && IsDefinition(image, layout, heaps, span) &&
                image[at] == 1 && image[at + 1] == 0x1e && image[at + 2] == resultArgument &&
                image[at + 3] == 0x10 && image[at + 4] == 0x1e && image[at + 5] == inputArgument &&
                image[at + 6] == 8;
        }

        // Identify the platform string overload by its signature and arity,
        // including MethodDef calls from the original CoreLib.
        internal static int StringAsSpanArity(byte[] image, int[] layout, int[] heaps, int row, bool member)
        {
            if (row < 1) return 0;
            const string space = "System";
            const string type = "MemoryExtensions";
            const string method = "AsSpan";
            int blob;
            if (member)
            {
                var reference = new int[AssemblyReferences.SymbolWidth];
                if (AssemblyReferences.ReadMember(image, heaps, layout, row, reference) < 0 ||
                    !MatchesAscii(image, heaps, (uint)reference[4], method)) return 0;
                var counts = new int[45];
                if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0 ||
                    !MatchReference(image, layout, heaps, counts, reference, space, type, includeMemory: true)) return 0;
                blob = reference[5];
            }
            else
            {
                if (!MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), method) ||
                    (MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x10) == 0) return 0;
                var counts = new int[45];
                int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
                if (first < 0 || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts)) return 0;
                int owner = ObjectLayouts.Owner(image, heaps, layout, row, false);
                if (!ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, owner, space, type)) return 0;
                blob = MethodDefinitions.ReadColumn(image, layout, row, 4);
            }

            if (blob < 1 || blob >= heaps[1]) return 0;
            var next = new int[1];
            int limit = heaps[0] + heaps[1];
            int size = CompressedUnsigned(image, heaps[0] + blob, limit, next);
            int start = next[0];
            if (size < 8 || size > limit - start || image[start] != 0 ||
                image[start + 2] != 0x15 || image[start + 3] != 0x11) return 0;
            int arity = image[start + 1];
            if (arity < 1 || arity > 3) return 0;
            int end = start + size;
            int span = CompressedUnsigned(image, start + 4, end, next);
            int at = next[0];
            if (span < 1 || end - at != arity + 2 || image[at] != 1 || image[at + 1] != 3 ||
                image[at + 2] != 0x0e || !IsDefinition(image, layout, heaps, span)) return 0;
            for (int operand = 1; operand < arity; operand++)
                if (image[at + 2 + operand] != SignatureReader.Int32) return 0;
            return arity;
        }

        internal static int ClassifySpanCall(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int member, int context, int[] methodArguments, int[] objects, bool constructor)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, member, reference, allowClosedParent: true) < 0)
                return 0;
            int parent = reference[AssemblyReferences.GenericParent];
            if (parent == 0) return 0;
            if (!MatchesAscii(image, heaps, (uint)reference[4], constructor ? ".ctor" : "op_Implicit")) return 0;
            int type = ManagedReferenceSignatures.ReadInstantiation(bundle, image, layout, heaps,
                owner, parent, objects, context, methodArguments);
            if (type != WritableCharacterSpan && type != ReadOnlyByteSpan) return 0;

            int blob = reference[5];
            if (blob < 1 || blob >= heaps[1]) return 0;
            int limit = heaps[0] + heaps[1];
            var next = new int[1];
            int size = CompressedUnsigned(image, heaps[0] + blob, limit, next);
            int start = next[0];
            if (size < 3 || size > limit - start) return 0;
            if (constructor)
            {
                // instance void .ctor(void*, int).
                if (type == WritableCharacterSpan && size == 6 && U32(image, (uint)start) == 0x0f010220 &&
                    U16(image, (uint)(start + 4)) == 0x0801) return BranchIl.NativeCharacterSpan;
                // instance void .ctor(!0[]).
                return size == 6 && U32(image, (uint)start) == 0x1d010120 &&
                    U16(image, (uint)(start + 4)) == 0x0013
                    ? type == ReadOnlyByteSpan ? BranchIl.ByteArrayAsSpan : BranchIl.CharacterArrayAsSpan : 0;
            }

            if (image[start] != 0 || image[start + 1] != 1) return 0;
            int end = start + size;
            int arrayParameter = OpenSpan(image, layout, heaps, start + 2, end, next, type == WritableCharacterSpan);
            if (arrayParameter >= 0 && end - arrayParameter == 3 && image[arrayParameter] == 0x1d &&
                image[arrayParameter + 1] == 0x13 && image[arrayParameter + 2] == 0)
                return type == ReadOnlyByteSpan ? BranchIl.ByteArrayAsSpan : BranchIl.CharacterArrayAsSpan;
            if (type == ReadOnlyByteSpan) return 0;

            int parameter = OpenSpan(image, layout, heaps, start + 2, end, next, false);
            if (parameter < 0) return 0;
            return OpenSpan(image, layout, heaps, parameter, end, next, true) == end
                ? BranchIl.SpanAsReadOnly : 0;
        }

        // The MemberRef signature uses the declaring Span<T> argument. Its
        // TypeSpec parent has already proved the closed element argument.
        internal static int OpenSpan(byte[] image, int[] layout, int[] heaps,
            int at, int end, int[] next, bool writable)
        {
            if (end - at < 6 || image[at] != 0x15 || image[at + 1] != 0x11) return -2;
            int span = CompressedUnsigned(image, at + 2, end, next);
            at = next[0];
            if (span <= 0 || end - at < 3 || image[at] != 1 ||
                image[at + 1] != 0x13 || image[at + 2] != 0 || !IsDefinition(image, layout, heaps, span, writable))
                return -2;
            return at + 3;
        }

        internal static bool IsSpanIndexOfAny(byte[] image, int[] layout, int[] heaps, int[] counts, int target)
        {
            int row = target >> 1;
            int blob;
            if ((target & 1) == 0)
            {
                if (row < 1 || row > counts[6] ||
                    !MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), "IndexOfAny") ||
                    (MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x10) == 0) return false;
                int owner = ObjectLayouts.Owner(image, heaps, layout, row, false);
                if (owner < 1 || !IsPlatformType(image, layout, heaps, owner << 2, "System", "MemoryExtensions"))
                    return false;
                blob = MethodDefinitions.ReadColumn(image, layout, row, 4);
            }
            else
            {
                var reference = new int[AssemblyReferences.SymbolWidth];
                if (AssemblyReferences.ReadMember(image, heaps, layout, row, reference) < 0 ||
                    !MatchesAscii(image, heaps, (uint)reference[4], "IndexOfAny") ||
                    !MatchReference(image, layout, heaps, counts, reference, "System", "MemoryExtensions", includeMemory: true))
                    return false;
                blob = reference[5];
            }
            var cursor = new int[1];
            int limit = heaps[0] + heaps[1];
            if (blob < 1 || blob >= heaps[1]) return false;
            int size = CompressedUnsigned(image, heaps[0] + blob, limit, cursor);
            int at = cursor[0];
            // static int IndexOfAny<T>(ReadOnlySpan<T>, T, T)
            if (size < 14 || size > limit - at || U32(image, (uint)at) != 0x08030110) return false;
            cursor[0] = at + 4;
            int end = at + size;
            int span = SignatureReader.ReadSingleArgumentValue(image, cursor, end);
            if (span < 1 || !IsDefinition(image, layout, heaps, span)) return false;
            for (int element = 0; element < 3; element++)
                if (!SignatureReader.TakeElement(image, cursor, end, 0x1e) ||
                    !SignatureReader.TakeElement(image, cursor, end, 0)) return false;
            return cursor[0] == end;
        }

        internal static bool IsCharacterCopyTo(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int member, int context, int[] methodArguments, int sourceType, int[] objects)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, member, reference, allowClosedParent: true) < 0 ||
                !MatchesAscii(image, heaps, (uint)reference[4], "CopyTo")) return false;
            int parent = reference[AssemblyReferences.GenericParent];
            if (parent == 0 || ManagedReferenceSignatures.ReadInstantiation(bundle, image, layout, heaps, owner,
                parent, objects, context, methodArguments) != sourceType) return false;
            var cursor = new int[1];
            int end = SignatureReader.ReadInstance(image, heaps, reference[5], 1, cursor);
            return end >= 0 && SignatureReader.TakeElement(image, cursor, end, SignatureReader.Void) &&
                OpenSpan(image, layout, heaps, cursor[0], end, cursor, true) == end;
        }

        // A negative result denotes inequality on the same verified span type.
        internal static int CharacterEquality(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int member, int context, int[] methodArguments, int[] objects)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, member, reference, allowClosedParent: true) < 0)
                return 0;
            int parent = reference[AssemblyReferences.GenericParent];
            if (parent == 0) return 0;
            uint name = (uint)reference[4];
            bool unequal = MatchesAscii(image, heaps, name, "op_Inequality");
            if (!unequal && !MatchesAscii(image, heaps, name, "op_Equality")) return 0;
            int type = ManagedReferenceSignatures.ReadInstantiation(bundle, image, layout, heaps, owner,
                parent, objects, context, methodArguments);
            bool writable = type == WritableCharacterSpan;
            if (!writable && type != CharacterSpan) return 0;
            var cursor = new int[1];
            int end = SignatureReader.ReadStatic(image, heaps, reference[5], 2, cursor);
            if (end < 0 || !SignatureReader.TakeElement(image, cursor, end, SignatureReader.Boolean)) return 0;
            int second = OpenSpan(image, layout, heaps, cursor[0], end, cursor, writable);
            return second >= 0 && OpenSpan(image, layout, heaps, second, end, cursor, writable) == end
                ? unequal ? -type : type : 0;
        }

        internal static bool IsSingleObjectConstructor(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int member, int context, int[] methodArguments, int[] objects)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, member, reference, allowClosedParent: true) < 0 ||
                reference[AssemblyReferences.GenericParent] == 0 ||
                !MatchesAscii(image, heaps, (uint)reference[4], ".ctor")) return false;
            uint blob = (uint)reference[5], at = (uint)heaps[0] + blob;
            // instance void .ctor(!0&). The parent is exactly the platform ReadOnlySpan<object>.
            if (!Fits(blob, 7, (uint)heaps[1]) || image[at] != 6 ||
                U32(image, at + 1) != 0x10010120 || U16(image, at + 5) != 0x0013) return false;
            return ManagedReferenceSignatures.ReadInstantiation(bundle, image, layout, heaps, owner,
                reference[AssemblyReferences.GenericParent], objects, context, methodArguments) == ObjectSpan;
        }

        internal static bool IsBytePointerConstructor(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int member, int context, int[] methodArguments, int[] objects)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, member, reference, allowClosedParent: true) < 0 ||
                reference[AssemblyReferences.GenericParent] == 0 ||
                !MatchesAscii(image, heaps, (uint)reference[4], ".ctor")) return false;
            uint blob = (uint)reference[5], at = (uint)heaps[0] + blob;
            // instance void .ctor(void*, int32), on the platform ReadOnlySpan<byte>.
            if (!Fits(blob, 7, (uint)heaps[1]) || image[at] != 6 ||
                U32(image, at + 1) != 0x0f010220 || U16(image, at + 5) != 0x0801) return false;
            return ManagedReferenceSignatures.ReadInstantiation(bundle, image, layout, heaps, owner,
                reference[AssemblyReferences.GenericParent], objects, context, methodArguments) == ReadOnlyByteSpan;
        }

        internal static bool IsSlice(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int member, int context, int[] methodArguments, int spanType, int[] objects, int arity)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, member, reference, allowClosedParent: true) < 0 ||
                !MatchesAscii(image, heaps, (uint)reference[4], "Slice")) return false;
            int parent = reference[AssemblyReferences.GenericParent];
            if (parent == 0 || ManagedReferenceSignatures.ReadInstantiation(bundle, image, layout, heaps,
                owner, parent, objects, context, methodArguments) != spanType) return false;
            var cursor = new int[1];
            int end = SignatureReader.ReadInstance(image, heaps, reference[5], arity, cursor);
            if (end < 0) return false;
            int result = SignatureReader.ReadGenericValue(image, cursor, end, parameter: 0);
            if (result < 1 || !IsDefinition(image, layout, heaps, result, spanType == WritableCharacterSpan))
                return false;
            for (int argument = 0; argument < arity; argument++)
                if (!SignatureReader.TakeElement(image, cursor, end, SignatureReader.Int32)) return false;
            return cursor[0] == end;
        }

        internal static bool IsToString(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int member, int context, int[] methodArguments, int spanType, int[] objects)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, member, reference, allowClosedParent: true) < 0 ||
                !MatchesAscii(image, heaps, (uint)reference[4], "ToString")) return false;
            int parent = reference[AssemblyReferences.GenericParent];
            if (parent == 0 || ManagedReferenceSignatures.ReadInstantiation(bundle, image, layout, heaps, owner,
                parent, objects, context, methodArguments) != spanType) return false;
            var cursor = new int[1];
            int end = SignatureReader.ReadInstance(image, heaps, reference[5], 0, cursor);
            return end >= 0 && SignatureReader.TakeElement(image, cursor, end, 0x0e) && cursor[0] == end;
        }

        internal static int Accessor(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int member, int context, int[] methodArguments, int spanType, int[] objects)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, member, reference, allowClosedParent: true) < 0)
                return 0;
            int parent = reference[AssemblyReferences.GenericParent];
            if (parent == 0) return 0;
            uint name = (uint)reference[4];
            bool length = MatchesAscii(image, heaps, name, "get_Length");
            bool empty = MatchesAscii(image, heaps, name, "get_IsEmpty");
            if (!length && !empty && !MatchesAscii(image, heaps, name, "get_Item")) return 0;
            if (ManagedReferenceSignatures.ReadInstantiation(bundle, image, layout, heaps,
                owner, parent, objects, context, methodArguments) != spanType) return 0;
            int blob = reference[5];
            if (length || empty)
            {
                var cursor = new int[1];
                int signatureEnd = SignatureReader.ReadInstance(image, heaps, blob, 0, cursor);
                int result = length ? SignatureReader.Int32 : SignatureReader.Boolean;
                return signatureEnd >= 0 && SignatureReader.TakeElement(image, cursor, signatureEnd, result) && cursor[0] == signatureEnd
                    ? length ? BranchIl.SpanLength : BranchIl.SpanIsEmpty : 0;
            }
            if (blob < 1 || blob >= heaps[1]) return 0;
            var next = new int[1];
            int limit = heaps[0] + heaps[1];
            int size = CompressedUnsigned(image, heaps[0] + blob, limit, next);
            int at = next[0];
            if (size < 3 || size > limit - at || image[at] != 0x20) return 0;
            if (spanType == WritableCharacterSpan)
                return size == 6 && U32(image, (uint)at) == 0x13100120 &&
                    U16(image, (uint)(at + 4)) == 0x0800 ? BranchIl.SpanAddress : 0;
            if (image[at + 1] != 1 || image[at + 2] != 0x1f) return 0;
            int end = at + size;
            int modifier = CompressedUnsigned(image, at + 3, end, next);
            if (modifier < 1 || end - next[0] != 4 ||
                !IsPlatformType(image, layout, heaps, modifier, "System.Runtime.InteropServices", "InAttribute") ||
                U32(image, (uint)next[0]) != 0x08001310) return 0;
            return BranchIl.SpanRead;
        }
    }
}
