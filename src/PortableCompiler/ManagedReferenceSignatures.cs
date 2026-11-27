using PeLibrary;
using static PeLibrary.ImageBytes;
using static PeLibrary.SignatureReader;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class ManagedReferenceSignatures
    {
        internal static int ReadStorage(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int[] next, int end, int context, int[] objects, int[] methodArguments = null) =>
            ObjectLayouts.SignatureStorage(objects, Read(bundle, image, layout, heaps, owner,
                next, end, context, objects, 0, methodArguments: methodArguments));

        // Read the element identity before mapping its by-reference ABI.
        // Boolean and Int32 share Wasm i32 values but have different locations.
        internal static int ReadLocation(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int[] next, int end, int context, int[] objects, int[] methodArguments = null)
        {
            int at = next[0];
            if (at >= end) return -1;
            int element = image[at];
            if (element != 2 && element != 3 && element != 5 && element != 6 && element != 8 && element != 0x0e && element != 0x1c &&
                element != 0x11 && element != 0x12 && element != 0x13 && element != 0x15 && element != 0x18 && element != 0x1e) return -2;
            int type = Read(bundle, image, layout, heaps, owner, next, end, context, objects, 0,
                methodArguments: methodArguments);
            if (type < 0) return type;
            int location = ElementLocation(objects, type);
            return location != 0 ? location : -2;
        }

        internal static int ElementLocation(int[] objects, int type)
        {
            int location = LocationFromIdentity(objects, type);
            // A managed out enum with 32-bit storage uses the existing scalar cell.
            if (location == 0 && ObjectLayouts.IsEnumKey(type) && ObjectLayouts.SignatureStorage(objects, type) == I32)
                location = Int32Location;
            return location;
        }

        internal static int TypeSpecLocation(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int row, int context, int[] objects, int[] methodArguments)
        {
            int type = TypeSpecIdentity(bundle, image, layout, heaps, owner, row, context, objects, methodArguments);
            if (type < 0) return type;
            int location = LocationFromIdentity(objects, type);
            return location != 0 ? location : -2;
        }

        internal static int TypeSpecIdentity(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int row, int context, int[] objects, int[] methodArguments)
        {
            int blob = AssemblyReferences.TypeSpecBlob(image, heaps, layout, row);
            if (blob < 0) return blob;
            int limit = heaps[0] + heaps[1];
            var next = new int[1];
            int size = CompressedUnsigned(image, heaps[0] + blob, limit, next);
            int start = next[0];
            if (size < 1 || size > limit - start) return -1;
            int end = start + size;
            int type = Read(bundle, image, layout, heaps, owner, next, end, context, objects, 0, methodArguments: methodArguments);
            return type < 0 || next[0] == end ? type : -1;
        }

        internal static int ReadInstantiation(BundleImages bundle, byte[] image,
            int[] layout, int[] heaps, int owner, int blob, int[] objects, int context = 0, int[] methodArguments = null)
        {
            // context is the closed layout whose arguments substitute VAR in
            // the TypeSpec, such as Holder<!0> referenced from Holder<int>.
            int limit = heaps[0] + heaps[1];
            var next = new int[1];
            int size = CompressedUnsigned(image, heaps[0] + blob, limit, next);
            int start = next[0];
            if (size < 1 || size > limit - start) return -1;
            if (image[start] != 0x15) return -2;
            int end = start + size;
            int type = Read(bundle, image, layout, heaps, owner, next, end, context, objects, 0, methodArguments: methodArguments);
            return type < 0 || next[0] == end ? type : -1;
        }

        internal static int Read(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int[] next, int end, int context, int[] objects, int depth, int[] substitutions = null, int[] methodArguments = null)
        {
            int at = next[0];
            if (at >= end) return -1;
            if (depth >= ObjectLayouts.MaxTypeDepth) return -2;
            int element = image[at];
            next[0] = at + 1;
            int scalar = GenericScalar.Key(element);
            if (scalar != 0) return scalar;
            if (element == 0x11)
            {
                int token = CompressedUnsigned(image, at + 1, end, next);
                if (token <= 0) return -1;
                int primitive = ArrayTypeRefs.PrimitiveElement(image, heaps, layout, token);
                if (primitive > 0) return GenericScalar.Key(primitive);
                if (primitive != -2) return primitive;

                int type = BundleLayouts.RegisterType(bundle, image, layout, heaps, owner, token, objects, true, true);
                return type;
            }
            if (element == 0x0e) return String;
            if (element == 0x1c) return ObjectLayouts.ObjectReference;
            if (element == 0x12 || element == 0x13 || element == 0x1e)
            {
                int token = CompressedUnsigned(image, at + 1, end, next);
                if (token < 0) return -1;
                if (element == 0x1e)
                    return methodArguments != null && token < methodArguments.Length ? methodArguments[token] : -2;
                if (element == 0x13)
                {
                    if (substitutions == null) return ObjectLayouts.Argument(objects, context, token);
                    return token < substitutions.Length ? substitutions[token] : -2;
                }
                return BundleLayouts.RegisterReference(bundle, image, layout, heaps, owner, token, objects);
            }
            if (element == 0x1d)
            {
                int item = Read(bundle, image, layout, heaps, owner, next, end, context, objects, depth + 1, substitutions, methodArguments);
                if (item < 0) return item;
                if (IsSpan(item)) return -2; // Byref-like values cannot be array elements.
                return GenericScalar.IsScalar(item) ? GenericScalar.VectorKey(item) : ObjectLayouts.RegisterArray(objects, item);
            }
            if (element != 0x15) return -2;
            var head = new int[2];
            int count = ReadGenericHeader(image, next, end, head);
            if (count < 0) return count;
            var arguments = new int[count];
            int status = ReadArguments(bundle, image, layout, heaps, owner, next, end, context, objects,
                depth + 1, substitutions, arguments, methodArguments);
            if (status < 0) return status;
            if (head[1] == 0x11 && count == 1)
            {
                int type = arguments[0] == ObjectLayouts.ObjectReference ? ObjectSpan :
                    arguments[0] == GenericScalar.Key(3) ? CharacterSpan :
                    arguments[0] == GenericScalar.Key(5) ? ReadOnlyByteSpan : 0;
                if (type != 0 && SpanMetadata.IsDefinition(image, layout, heaps, head[0])) return type;
                if (type == CharacterSpan && SpanMetadata.IsDefinition(image, layout, heaps, head[0], writable: true))
                    return WritableCharacterSpan;
            }
            if (head[1] == 0x12 && count == 2 && arguments[0] == GenericScalar.Key(3) &&
                arguments[1] == GenericScalar.Key(0x18) &&
                SpanMetadata.IsPlatformType(image, layout, heaps, head[0], "System.Buffers", "SpanAction`2"))
                return CharacterSpanCallback;
            if (head[1] == 0x12)
            {
                // Stage 204 (decision 0091): System.Func`n / Action`n instantiations get a delegate key.
                int general = GeneralDelegates.Storage(image, layout, heaps, head[0], arguments, count, objects);
                if (general != 0) return general;
            }
            return BundleLayouts.RegisterInstantiation(bundle, image, layout, heaps, owner, head[0], objects, arguments, head[1] == 0x11);
        }

        // TypeSpec extends is read while the child has no layout. Interpret
        // VAR in the child's argument vector and reserve the parent only.
        internal static int ReserveBase(BundleImages bundle, byte[] image, int[] layout,
            int[] heaps, int owner, int row, int[] objects, int[] substitutions)
        {
            int blob = AssemblyReferences.TypeSpecBlob(image, heaps, layout, row);
            if (blob < 0) return blob;
            int limit = heaps[0] + heaps[1];
            var next = new int[1];
            int size = CompressedUnsigned(image, heaps[0] + blob, limit, next);
            int start = next[0];
            if (size < 1 || size > limit - start) return -1;
            if (image[start] != 0x15) return -2;
            int end = start + size;
            next[0] = start + 1;
            var head = new int[2];
            int count = ReadGenericHeader(image, next, end, head);
            if (count < 0) return count;
            if (head[1] != 0x12) return -2;
            var arguments = new int[count];
            int status = ReadArguments(bundle, image, layout, heaps, owner, next, end, 0, objects, 0, substitutions, arguments, null);
            if (status < 0) return status;
            if (next[0] != end) return -1;
            return BundleLayouts.ReserveParent(bundle, image, layout, heaps, owner, head[0], objects, arguments);
        }

        private static int ReadArguments(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int[] cursor, int end, int context, int[] objects, int depth, int[] substitutions, int[] arguments, int[] methodArguments)
        {
            for (int i = 0; i < arguments.Length; i++)
            {
                int argument = Read(bundle, image, layout, heaps, owner, cursor, end, context, objects, depth, substitutions, methodArguments);
                if (argument < 0) return argument;
                arguments[i] = argument;
            }
            return 0;
        }
    }
}
