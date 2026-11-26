using PeLibrary;

namespace PortableCompiler
{
    internal static class PointerSignatures
    {
        internal static int ReadStorage(BundleImages bundle, byte[] image, int[] layout,
            int[] heaps, int owner, int position, int end, int[] next)
        {
            int pointee = PointerStorage.ReadPointee(image, position, end, next);
            if (pointee < 0) return pointee;
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

        // Returns the right cursor and leaves the left cursor in next.
        internal static int Match(BundleImages bundle, byte[] caller, int[] callerLayout,
            int[] callerHeaps, int callerOwner, byte[] target, int[] targetLayout, int[] targetHeaps, int targetOwner,
            int left, int leftEnd, int right, int rightEnd, int[] leftClass, int[] rightClass, int[] next)
        {
            // Depth and terminal element encoding both participate in binding.
            while (left < leftEnd && right < rightEnd && caller[left] == PointerStorage.Element &&
                target[right] == PointerStorage.Element)
            {
                left++;
                right++;
            }
            if (left >= leftEnd || right >= rightEnd || caller[left] != target[right]) return 0;

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
