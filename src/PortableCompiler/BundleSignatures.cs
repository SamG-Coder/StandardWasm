using PeLibrary;
using static PeLibrary.ImageBytes;

namespace PortableCompiler
{
    internal static class BundleSignatures
    {
        internal static int Match(BundleImages bundle,
            byte[] caller, int[] callerLayout, int[] callerHeaps, int callerOwner, int callerSignature,
            byte[] target, int[] targetLayout, int[] targetHeaps, int targetOwner, int targetSignature, bool field, int[] objects, int context = 0)
        {
            var next = new int[1];
            int left = Start(caller, callerHeaps, callerSignature, next, field);
            if (left < 0) return left;
            int leftEnd = next[0];
            int right = Start(target, targetHeaps, targetSignature, next, field);
            if (right < 0) return 0;
            int rightEnd = next[0];
            if (caller[left - 1] != target[right - 1]) return 0;

            // A field has exactly one type, without a parameter count.
            int count = 0;
            if (field)
            {
                next[0] = left;
                int leftModifier = ObjectLayouts.ReadFieldModifier(caller, callerHeaps, callerLayout, next, leftEnd);
                if (leftModifier < 0) return leftModifier;
                left = next[0];
                next[0] = right;
                int rightModifier = ObjectLayouts.ReadFieldModifier(target, targetHeaps, targetLayout, next, rightEnd);
                if (leftModifier != rightModifier) return 0;
                right = next[0];
            }
            else
            {
                count = CompressedUnsigned(caller, left, leftEnd, next);
                left = next[0];
                int candidateCount = CompressedUnsigned(target, right, rightEnd, next);
                right = next[0];
                if (count < 0 || count >= leftEnd - left) return -1;
                if (count != candidateCount || count >= rightEnd - right) return 0;
            }

            var leftClass = new int[2];
            var rightClass = new int[2];
            for (int i = 0; i <= count; i++)
            {
                if (left >= leftEnd || right >= rightEnd) return 0;
                int element = caller[left++];
                int other = target[right++];
                if (element != other)
                {
                    if (element == 0x12 && other == 0x1c)
                    {
                        int token = CompressedUnsigned(caller, left, leftEnd, next);
                        left = next[0];
                        int status = ObjectIdentity(bundle, caller, callerLayout, callerHeaps,
                            callerOwner, token, leftClass);
                        if (status <= 0) return status;
                    }
                    else if (element == 0x1c && other == 0x12)
                    {
                        int token = CompressedUnsigned(target, right, rightEnd, next);
                        right = next[0];
                        int status = ObjectIdentity(bundle, target, targetLayout, targetHeaps,
                            targetOwner, token, rightClass);
                        if (status <= 0) return status;
                    }
                    else return 0;
                    continue;
                }
                if (element == 0x12 || element == 0x11)
                {
                    int leftToken = CompressedUnsigned(caller, left, leftEnd, next);
                    left = next[0];
                    int rightToken = CompressedUnsigned(target, right, rightEnd, next);
                    right = next[0];

                    // Platform EventArgs and EventHandler are compiler-owned and
                    // named by identity through any platform assembly (System.Runtime,
                    // netstandard, CoreLib), which need not be bundled.
                    int leftPlatform = element == 0x12 ? EventMetadata.Reference(caller, callerHeaps, callerLayout, leftToken) : 0;
                    int rightPlatform = element == 0x12 ? EventMetadata.Reference(target, targetHeaps, targetLayout, rightToken) : 0;
                    if (leftPlatform != 0 || rightPlatform != 0)
                    {
                        if (leftPlatform != rightPlatform) return 0;
                        continue;
                    }
                    if (!field)
                    {
                        int leftRuntime = RuntimeTypeMetadata.Storage(caller, callerHeaps, callerLayout, leftToken, element == 0x11);
                        int rightRuntime = RuntimeTypeMetadata.Storage(target, targetHeaps, targetLayout, rightToken, element == 0x11);
                        // When one side is a TypeDef, use the ordinary nominal binder.
                        if (leftRuntime != 0 && rightRuntime != 0)
                        {
                            if (leftRuntime != rightRuntime) return 0;
                            continue;
                        }
                    }

                    // Local coded indices can differ even when the classes are
                    // identical. Match the resolved assembly and TypeDef row.
                    int status = AssemblyBundle.ClassIdentity(bundle, caller, callerLayout,
                        callerHeaps, callerOwner, leftToken, leftClass);
                    if (status < 0) return status;
                    status = AssemblyBundle.ClassIdentity(bundle, target, targetLayout,
                        targetHeaps, targetOwner, rightToken, rightClass);
                    if (status < 0 || leftClass[0] != rightClass[0] || leftClass[1] != rightClass[1])
                        return 0;
                }
                else if (element == 0x1d)
                {
                    // Exact element encodings distinguish signed and unsigned arrays.
                    if (left >= leftEnd || right >= rightEnd) return 0;
                    if (ArrayTypeRefs.Storage(caller[left]) != 0 && ArrayTypeRefs.Storage(target[right]) != 0)
                    {
                        if (caller[left++] != target[right++]) return 0;
                        continue;
                    }
                    int match = MatchReference(bundle, caller, callerLayout, callerHeaps, callerOwner,
                        target, targetLayout, targetHeaps, targetOwner, left - 1, leftEnd, right - 1, rightEnd, objects, next, context);
                    if (match <= 0) return match;
                    left = next[0];
                    right = match;
                }
                else if (element == 0x15 || element == 0x13 && context != 0)
                {
                    int match = MatchReference(bundle, caller, callerLayout, callerHeaps, callerOwner,
                        target, targetLayout, targetHeaps, targetOwner, left - 1, leftEnd, right - 1, rightEnd, objects, next, context);
                    if (match <= 0) return match;
                    left = next[0];
                    right = match;
                }
                else if (element == 0x10 && !field && i > 0)
                {
                    if (left >= leftEnd || right >= rightEnd || caller[left] != target[right]) return 0;
                    int byRefElement = caller[left];
                    if (byRefElement == 0x13 || byRefElement == 0x1e)
                    {
                        // Preserve overload identity even when two variables substitute the same type.
                        int leftIndex = CompressedUnsigned(caller, left + 1, leftEnd, next);
                        int rightIndex = CompressedUnsigned(target, right + 1, rightEnd, next);
                        if (leftIndex < 0) return -1;
                        if (leftIndex != rightIndex) return 0;
                    }
                    next[0] = left;
                    int leftType = ManagedReferenceSignatures.ReadLocation(bundle, caller, callerLayout, callerHeaps,
                        callerOwner, next, leftEnd, context, objects);
                    left = next[0];
                    next[0] = right;
                    int rightType = ManagedReferenceSignatures.ReadLocation(bundle, target, targetLayout, targetHeaps,
                        targetOwner, next, rightEnd, context, objects);
                    if (leftType == -1) return -1;
                    if (leftType < 0 || leftType != rightType) return 0;
                    right = next[0];
                }
                else if (element == PointerStorage.Element)
                {
                    int match = PointerSignatures.Match(bundle, caller, callerLayout, callerHeaps,
                        callerOwner, target, targetLayout, targetHeaps, targetOwner, left, leftEnd, right, rightEnd,
                        leftClass, rightClass, next);
                    if (match <= 0) return match;
                    left = next[0];
                    right = match;
                }
                else if ((element < 1 || element > 0x0e) && !NativeStorage.IsIntegerElement(element) && element != 0x1c)
                    return 0;
            }
            return left == leftEnd && right == rightEnd ? 1 : 0;
        }

        private static int MatchReference(BundleImages bundle, byte[] caller, int[] callerLayout,
            int[] callerHeaps, int callerOwner, byte[] target, int[] targetLayout, int[] targetHeaps, int targetOwner,
            int left, int leftEnd, int right, int rightEnd, int[] objects, int[] next, int context)
        {
            int leftStart = left, rightStart = right;
            next[0] = left;
            int leftType = ManagedReferenceSignatures.Read(bundle, caller, callerLayout, callerHeaps,
                callerOwner, next, leftEnd, context, objects, 0);
            if (leftType < 0) return leftType;
            left = next[0];
            next[0] = right;
            int rightType = ManagedReferenceSignatures.Read(bundle, target, targetLayout, targetHeaps,
                targetOwner, next, rightEnd, context, objects, 0);
            right = next[0];
            next[0] = left;
            if (leftType != rightType) return 0;

            // Layout canonicalizes named primitives. Member linkage still
            // requires the primitive spelling used by the definition.
            if (!SamePrimitiveEncoding(caller, target, leftStart, left, rightStart, right)) return 0;
            return right;
        }

        private static bool SamePrimitiveEncoding(byte[] left, byte[] right, int l, int leftEnd, int r, int rightEnd)
        {
            var cursor = new int[1];
            while (l < leftEnd && r < rightEnd)
            {
                int a = left[l++], b = right[r++];
                if ((a == 0x11 && GenericScalar.Key(b) != 0) || (b == 0x11 && GenericScalar.Key(a) != 0))
                    return false;
                l = NextTypeNode(left, l, leftEnd, a, cursor);
                r = NextTypeNode(right, r, rightEnd, b, cursor);
            }
            return l == leftEnd && r == rightEnd;
        }

        private static int NextTypeNode(byte[] image, int at, int end, int kind, int[] cursor)
        {
            // The signature reader already validated these tokens and counts.
            if (kind == 0x15)
            {
                CompressedUnsigned(image, at + 1, end, cursor); // Declaring TypeDefOrRef.
                CompressedUnsigned(image, cursor[0], end, cursor); // Argument count.
                return cursor[0];
            }
            if (kind == 0x11 || kind == 0x12 || kind == 0x13)
            {
                CompressedUnsigned(image, at, end, cursor);
                return cursor[0];
            }
            return at;
        }

        private static int ObjectIdentity(BundleImages bundle, byte[] image,
            int[] layout, int[] heaps, int owner, int coded, int[] symbol)
        {
            int status = AssemblyBundle.ClassIdentity(bundle, image, layout, heaps, owner, coded, symbol);
            if (status < 0) return status;
            return symbol[0] == AssemblyBundle.ObjectOwner ? 1 : 0;
        }

        private static int Start(byte[] image, int[] heaps, int index, int[] next, bool field)
        {
            if (index < 1 || index >= heaps[1]) return -1;
            int limit = heaps[0] + heaps[1];
            int size = CompressedUnsigned(image, heaps[0] + index, limit, next);
            int first = next[0];
            // Ordinary methods use default calling convention with optional
            // implicit this. The receiver is outside the parameter list.
            if (size < (field ? 2 : 3) || size > limit - first) return -1;
            if (field ? image[first] != 6 : (image[first] & ~0x20) != 0) return -1;
            next[0] = first + size;
            return first + 1;
        }
    }
}
