using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    // Identifies the few platform TypeRefs whose members the compiler lowers
    // directly. Each match requires the namespace, the name and an authentic
    // platform AssemblyRef, so a user type with the same spelling never binds.
    internal static class PlatformTypeReferences
    {
        internal const int Console = 1, TextWriter = 2, String = 3, Type = 4, TypeHandle = 5, TypeBase = 6;

        internal static int Identify(byte[] image, int[] streams, int[] methods, int[] counts, int first, int row)
        {
            if (row < 1 || row > counts[1]) return 0;
            int strings = methods[2];
            int guid = (image[streams[4] + 6] & 2) == 0 ? 2 : 4;
            int refs = first + counts[0] * (2 + strings + 3 * guid);
            int scopeWidth = counts[0] < 16384 && counts[1] < 16384 && counts[26] < 16384 && counts[35] < 16384 ? 2 : 4;
            uint type = (uint)(refs + (row - 1) * (scopeWidth + 2 * strings));
            uint scope = Index(image, type, scopeWidth);
            if ((scope & 3) != 2) return 0; // ResolutionScope tag 2 is AssemblyRef.
            uint name = Index(image, type + (uint)scopeWidth, strings);
            uint space = Index(image, type + (uint)(scopeWidth + strings), strings);
            uint heap = (uint)streams[2], heapSize = (uint)streams[3];
            int assembly = (int)(scope >> 2);
            if (Named(image, heap, heapSize, space, 6, 0x74737953, 0x6d65, 0)) // System
            {
                if (Named(image, heap, heapSize, name, 7, 0x736e6f43, 0x656c6f, 0)) // Console
                    return PlatformAssemblyIdentity.IsConsoleAssembly(image, streams, methods, counts, assembly) ? Console : 0;
                if (Named(image, heap, heapSize, name, 6, 0x69727453, 0x676e, 0)) // String
                    return PlatformAssemblyIdentity.IsPlatformAssembly(image, streams, methods, counts, assembly) ? String : 0;
                if (Named(image, heap, heapSize, name, 4, 0x65707954, 0, 0)) // Type
                    return PlatformAssemblyIdentity.IsPlatformAssembly(image, streams, methods, counts, assembly) ? Type : 0;
                if (Named17(image, heap, heapSize, name, 0x746e7552, 0x54656d69, 0x48657079, 0x6c646e61, 0x65)) // RuntimeTypeHandle
                    return PlatformAssemblyIdentity.IsPlatformAssembly(image, streams, methods, counts, assembly) ? TypeHandle : 0;
                return 0;
            }
            if (Named(image, heap, heapSize, space, 9, 0x74737953, 0x492e6d65, 0x4f) && // System.IO
                Named(image, heap, heapSize, name, 10, 0x74786554, 0x74697257, 0x7265)) // TextWriter
                return PlatformAssemblyIdentity.IsPlatformAssembly(image, streams, methods, counts, assembly) ? TextWriter : 0;
            if (Named17(image, heap, heapSize, space, 0x74737953, 0x522e6d65, 0x656c6665, 0x6f697463, 0x6e) && // System.Reflection
                (Named(image, heap, heapSize, name, 10, 0x626d654d, 0x6e497265, 0x6f66) || // MemberInfo
                 Named(image, heap, heapSize, name, 8, 0x65707954, 0x6f666e49, 0))) // TypeInfo
                return PlatformAssemblyIdentity.IsPlatformAssembly(image, streams, methods, counts, assembly) ? TypeBase : 0;
            return 0;
        }

        // A TypeDefOrRef coded index naming the platform System.String.
        internal static bool IsString(byte[] image, int[] streams, int[] methods, int coded)
        {
            if ((coded & 3) != 1) return false;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, streams[4], streams[5], counts);
            return first >= 0 && Identify(image, streams, methods, counts, first, coded >> 2) == String;
        }

        // Compare a #Strings entry with up to twelve ASCII bytes packed
        // little-endian into three words, followed by the terminator.
        internal static bool Named(byte[] image, uint heap, uint size, uint index, int length, uint first, uint second, uint third)
        {
            if (index == 0 || !Fits(index, (uint)length + 1, size)) return false;
            uint at = heap + index;
            for (int i = 0; i < length; i++)
            {
                uint word = i < 4 ? first : i < 8 ? second : third;
                if (image[at + (uint)i] != unchecked((byte)(word >> ((i & 3) * 8)))) return false;
            }
            return image[at + (uint)length] == 0;
        }

        internal static bool Named17(byte[] image, uint heap, uint size, uint index, uint first, uint second, uint third,
            uint fourth, uint last)
        {
            if (index == 0 || !Fits(index, 18, size)) return false;
            uint at = heap + index;
            return U32(image, at) == first && U32(image, at + 4) == second && U32(image, at + 8) == third &&
                U32(image, at + 12) == fourth && U16(image, at + 16) == last;
        }

        private static uint Index(byte[] image, uint at, int width) => width == 2 ? U16(image, at) : U32(image, at);
    }
}
