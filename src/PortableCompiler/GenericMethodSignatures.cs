using PeLibrary;
using static PeLibrary.ImageBytes;

namespace PortableCompiler
{
    internal static class GenericMethodSignatures
    {
        internal static int Match(BundleImages bundle,
            byte[] caller, int[] callerLayout, int[] callerHeaps, int callerOwner, int callerSignature,
            byte[] target, int[] targetLayout, int[] targetHeaps, int targetOwner, int targetSignature, int arity, bool instance = false)
        {
            var left = new int[1];
            var right = new int[1];
            int leftEnd = Start(caller, callerHeaps, callerSignature, left, arity, instance);
            if (leftEnd <= 0) return leftEnd;
            int rightEnd = Start(target, targetHeaps, targetSignature, right, arity, instance);
            if (rightEnd <= 0) return 0;
            int parameters = CompressedUnsigned(caller, left[0], leftEnd, left);
            if (parameters < 0 || parameters >= leftEnd - left[0]) return -1;
            if (parameters != CompressedUnsigned(target, right[0], rightEnd, right)) return 0;

            var callerIdentity = new int[2];
            var targetIdentity = new int[2];
            int pending = parameters + 1; // Return type, then the parameters.
            // Type encodings are prefix trees. Each node consumes one pending
            // type, and a constructed type adds its children to that count.
            while (pending != 0)
            {
                if (pending > leftEnd - left[0] || pending > rightEnd - right[0]) return 0;
                int element = caller[left[0]];
                if (element != target[right[0]]) return 0;
                left[0] = left[0] + 1;
                right[0] = right[0] + 1;
                pending--;
                if (element == 0x13 || element == 0x1e)
                {
                    // Bind the open declarations before substituting arguments.
                    // Two distinct method variables can have the same closed type.
                    int index = CompressedUnsigned(caller, left[0], leftEnd, left);
                    if (index < 0 || element == 0x1e && index >= arity) return -1;
                    if (index != CompressedUnsigned(target, right[0], rightEnd, right)) return 0;
                }
                else if (element == 0x11 || element == 0x12 || element == 0x15 || element == 0x1f || element == 0x20)
                {
                    bool constructed = element == 0x15;
                    if (constructed)
                    {
                        if (left[0] >= leftEnd || right[0] >= rightEnd) return 0;
                        int kind = caller[left[0]];
                        if ((kind != 0x11 && kind != 0x12) || kind != target[right[0]]) return 0;
                        left[0] = left[0] + 1;
                        right[0] = right[0] + 1;
                    }
                    int leftToken = CompressedUnsigned(caller, left[0], leftEnd, left);
                    int rightToken = CompressedUnsigned(target, right[0], rightEnd, right);
                    if (leftToken <= 0 || rightToken <= 0) return 0;
                    int status = AssemblyBundle.ClassIdentity(bundle, caller, callerLayout, callerHeaps,
                        callerOwner, leftToken, callerIdentity);
                    if (status < 0) return status;
                    status = AssemblyBundle.ClassIdentity(bundle, target, targetLayout, targetHeaps,
                        targetOwner, rightToken, targetIdentity);
                    if (status < 0 || callerIdentity[0] != targetIdentity[0] || callerIdentity[1] != targetIdentity[1])
                        return 0;
                    // A custom modifier has its own nominal identity and
                    // leaves the modified type to be matched next.
                    if (element == 0x1f || element == 0x20) pending++;
                    if (constructed)
                    {
                        int arguments = CompressedUnsigned(caller, left[0], leftEnd, left);
                        if (arguments < 1 || arguments != CompressedUnsigned(target, right[0], rightEnd, right) ||
                            arguments > leftEnd - left[0] - pending)
                            return 0;
                        pending += arguments;
                    }
                }
                else if (element == 0x1d || element == PointerStorage.Element || element == 0x10)
                    pending++;
                else if ((element < 1 || element > 0x0e) && !NativeStorage.IsIntegerElement(element) && element != 0x1c)
                    return 0;
            }
            return left[0] == leftEnd && right[0] == rightEnd ? 1 : 0;
        }

        internal static int Start(byte[] image, int[] heaps, int index, int[] next, int arity, bool instance = false)
        {
            if (index <= 0 || index >= heaps[1]) return -1;
            int limit = heaps[0] + heaps[1];
            int size = CompressedUnsigned(image, heaps[0] + index, limit, next);
            int start = next[0];
            if (size < (arity == 0 ? 3 : 4) || size > limit - start) return -1;
            if (image[start] != ((arity == 0 ? 0 : 0x10) | (instance ? 0x20 : 0))) return 0;
            int end = start + size;
            next[0] = start + 1;
            if (arity == 0) return end;
            return CompressedUnsigned(image, next[0], end, next) == arity ? end : 0;
        }
    }
}
