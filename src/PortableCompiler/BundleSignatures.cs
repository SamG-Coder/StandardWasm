using PeLibrary;
using static PeLibrary.ImageBytes;

namespace PortableCompiler
{
    internal static class BundleSignatures
    {
        internal static int Match(byte[] main, byte[] bundle, int[] ranges,
            byte[] caller, int[] callerLayout, int[] callerHeaps, int callerOwner, int callerSignature,
            byte[] target, int[] targetLayout, int[] targetHeaps, int targetOwner, int targetSignature, bool field)
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
            if (!field)
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
                        int status = ObjectIdentity(main, bundle, ranges, caller, callerLayout, callerHeaps,
                            callerOwner, token, leftClass);
                        if (status <= 0) return status;
                    }
                    else if (element == 0x1c && other == 0x12)
                    {
                        int token = CompressedUnsigned(target, right, rightEnd, next);
                        right = next[0];
                        int status = ObjectIdentity(main, bundle, ranges, target, targetLayout, targetHeaps,
                            targetOwner, token, rightClass);
                        if (status <= 0) return status;
                    }
                    else return 0;
                    continue;
                }
                if (element == 0x12)
                {
                    int leftToken = CompressedUnsigned(caller, left, leftEnd, next);
                    left = next[0];
                    int rightToken = CompressedUnsigned(target, right, rightEnd, next);
                    right = next[0];

                    // Local coded indices can differ even when the classes are
                    // identical. Match the resolved assembly and TypeDef row.
                    int status = AssemblyBundle.ClassIdentity(main, bundle, ranges, caller, callerLayout,
                        callerHeaps, callerOwner, leftToken, leftClass);
                    if (status < 0) return status;
                    status = AssemblyBundle.ClassIdentity(main, bundle, ranges, target, targetLayout,
                        targetHeaps, targetOwner, rightToken, rightClass);
                    if (status < 0 || leftClass[0] != rightClass[0] || leftClass[1] != rightClass[1])
                        return 0;
                }
                else if (element == 0x1d)
                {
                    // Exact element encodings distinguish signed and unsigned arrays.
                    if (left >= leftEnd || right >= rightEnd || ArrayTypeRefs.Storage(caller[left]) == 0 ||
                        caller[left++] != target[right++]) return 0;
                }
                else if ((element < 1 || element > 0x0e) && element != 0x1c)
                    return 0;
            }
            return left == leftEnd && right == rightEnd ? 1 : 0;
        }

        private static int ObjectIdentity(byte[] main, byte[] bundle, int[] ranges, byte[] image,
            int[] layout, int[] heaps, int owner, int coded, int[] symbol)
        {
            int status = AssemblyBundle.ClassIdentity(main, bundle, ranges, image, layout, heaps, owner, coded, symbol);
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
