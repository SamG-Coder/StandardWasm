using PeLibrary;

namespace PortableCompiler
{
    internal static class PointerSignatures
    {
        internal static int ReadClosedStorage(BundleImages bundle, byte[] image, int[] layout,
            int[] heaps, int owner, int position, int end, int[] next, int[] objects, int context, int[] methodArguments)
        {
            if (position >= end || (image[position] != 0x13 && image[position] != 0x1e))
                return ReadStorage(bundle, image, layout, heaps, owner, position, end, next);

            // Use the substituted pointee before deciding whether the result
            // is a native address. A generic variable alone proves no layout.
            next[0] = position;
            int type = ManagedReferenceSignatures.Read(bundle, image, layout, heaps, owner,
                next, end, context, objects, 0, methodArguments: methodArguments);
            if (type < 0) return type;
            if (GenericScalar.IsScalar(type)) return SignatureTypes.Native;
            if (!ObjectLayouts.IsValueType(objects, type)) return -2;
            return NativeValueLayouts.Measure(bundle, ObjectLayouts.AssemblyOf(objects, type),
                ObjectLayouts.TypeDef(objects, type), new int[2]) == 0 ? SignatureTypes.Native : PointerStorage.NullPointer;
        }

        internal static int ReadStorage(BundleImages bundle, byte[] image, int[] layout,
            int[] heaps, int owner, int position, int end, int[] next)
        {
            // Decision 0060: a single-level pointer to a primitive or to void is an i32
            // address into memory 0, the CLI's native-int stack type. A byte* passes to a
            // void* parameter with no IL conversion, so both must share it (the original
            // UnmanagedMemoryStream calls ArgumentNullException.ThrowIfNull(void*, string)).
            // Pointers to named structs and multi-level pointers keep the null-only
            // storage of decisions 0032 and 0035.
            if (position < end && IsPrimitiveElement(image[position]))
            {
                next[0] = position + 1;
                return SignatureTypes.Native;
            }
            // Decision 0078: a pointer to a function pointer (delegate*<...>*) addresses a
            // native-size slot in memory 0, like a pointer to nint.
            if (position < end && image[position] == 0x1b)
            {
                int after = PeLibrary.SignatureExtents.Method(image, position + 1, end);
                if (after < 0) return -2;
                next[0] = after;
                return SignatureTypes.Native;
            }
            int pointee = PointerStorage.ReadPointee(image, position, end, next);
            if (pointee < 0) return pointee;
            if (pointee > 0 && image[position] == 0x11)
            {
                // Stage 143 (decision 0071): a single-level pointer to a blittable sequential struct is an address.
                var identity = new int[2];
                if (AssemblyBundle.ClassIdentity(bundle, image, layout, heaps, owner, pointee, identity) >= 0 &&
                    NativeValueLayouts.Measure(bundle, identity[0], identity[1], new int[2]) == 0)
                    return SignatureTypes.Native;
            }
            if (pointee > 0)
            {
                // The CLI type must bind even when the only supported value is
                // null. An opaque native struct needs no managed Wasm layout.
                int status = AssemblyBundle.ClassIdentity(bundle, image, layout, heaps, owner,
                    pointee, new int[2]);
                if (status < 0) return status;
            }
            return PointerStorage.NullPointer;
        }

        // void, bool, char, the sized integers, float, double and native ints.
        private static bool IsPrimitiveElement(int element) =>
            element >= 1 && element <= 0x0d || element == 0x18 || element == 0x19;

        // Returns the right cursor and leaves the left cursor in next.
        internal static int Match(BundleImages bundle, byte[] caller, int[] callerLayout,
            int[] callerHeaps, int callerOwner, byte[] target, int[] targetLayout, int[] targetHeaps, int targetOwner,
            int left, int leftEnd, int right, int rightEnd, int[] leftClass, int[] rightClass, int[] next,
            int[] objects, int context, int targetContext)
        {
            // Depth and terminal element encoding both participate in binding.
            while (left < leftEnd && right < rightEnd && caller[left] == PointerStorage.Element &&
                target[right] == PointerStorage.Element)
            {
                left++;
                right++;
            }
            if (left >= leftEnd || right >= rightEnd || caller[left] != target[right]) return 0;

            if (caller[left] == 0x13)
            {
                // Preserve variable ordinals for overload identity, even
                // when two arguments substitute the same closed type.
                int rightIndex = PeLibrary.ImageBytes.CompressedUnsigned(target, right + 1, rightEnd, next);
                if (rightIndex < 0) return 0;
                right = next[0];
                int leftIndex = PeLibrary.ImageBytes.CompressedUnsigned(caller, left + 1, leftEnd, next);
                if (leftIndex < 0) return -1;
                if (leftIndex != rightIndex) return 0;
                int type = ObjectLayouts.Argument(objects, context, leftIndex);
                return type >= 0 && type == ObjectLayouts.Argument(objects, targetContext, rightIndex) ? right : 0;
            }

            int rightPointee = PointerStorage.ReadPointee(target, right, rightEnd, next);
            if (rightPointee < 0) return 0;
            right = next[0];
            int leftPointee = PointerStorage.ReadPointee(caller, left, leftEnd, next);
            if (leftPointee < 0) return leftPointee;
            if (leftPointee > 0)
            {
                int status = AssemblyBundle.ClassIdentity(bundle, caller, callerLayout, callerHeaps,
                    callerOwner, leftPointee, leftClass);
                if (status < 0) return status;
                status = AssemblyBundle.ClassIdentity(bundle, target, targetLayout, targetHeaps,
                    targetOwner, rightPointee, rightClass);
                if (status < 0 || leftClass[0] != rightClass[0] || leftClass[1] != rightClass[1]) return 0;
            }
            return right;
        }
    }
}
