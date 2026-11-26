using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class ArrayTypeRefs
    {
        internal const byte Bytes = 2, Ints = 3, Packed16 = 7, Singles = 15, Doubles = 16;

        internal static byte Storage(int element)
        {
            if (element == 2 || element == 4 || element == 5) return Bytes;
            if (element == 3 || element == 6 || element == 7) return Packed16;
            if (element == 8 || element == 9) return Ints;
            if (WideStorage.IsIntegerElement(element)) return WideStorage.Array;
            if (element == 0x0c) return Singles;
            if (element == 0x0d) return Doubles;
            if (NativeStorage.IsIntegerElement(element)) return NativeStorage.Vector;
            return 0;
        }

        internal static int ElementType(byte[] image, int[] streams, int[] methods, uint token)
        {
            int row = (int)(token & 0xffffff);
            if (token >> 24 != 1 || row == 0)
                return -2;
            int element = PrimitiveElement(image, streams, methods, row << 2 | 1, includeRuntimeExtensions: true);
            if (element < 0) return element;
            int storage = Storage(element);
            return storage != 0 ? storage : -2;
        }

        internal static int PrimitiveElement(byte[] image, int[] streams, int[] methods, int coded, bool includeRuntimeExtensions = false)
        {
            int row = coded >> 2, tag = coded & 3;
            if (row <= 0 || tag > 1) return -2;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, streams[4], streams[5], counts);
            if (first < 0 || row > counts[tag == 1 ? 1 : 2])
                return -1;
            int strings = methods[2];
            int guid = (image[streams[4] + 6] & 2) == 0 ? 2 : 4;
            int moduleWidth = 2 + strings + 3 * guid;
            int scope = counts[0] < 16384 && counts[26] < 16384 && counts[35] < 16384 && counts[1] < 16384 ? 2 : 4;
            // MethodDef discovery already validated every preceding row.
            int refs = first + counts[0] * moduleWidth, refWidth = scope + 2 * strings;
            uint name, ns;
            if (tag == 1)
            {
                uint type = (uint)(refs + (row - 1) * refWidth);
                uint resolution = Index(image, type, scope);
                if ((resolution & 3) != 2 || !PlatformAssemblyIdentity.IsPlatformAssembly(image, streams,
                    methods, counts, (int)(resolution >> 2), includeRuntimeExtensions: includeRuntimeExtensions))
                    return -2;
                name = Index(image, type + (uint)scope, strings);
                ns = Index(image, type + (uint)(scope + strings), strings);
            }
            else
            {
                if (!PlatformAssemblyIdentity.IsCoreLibDefinition(image, streams, methods, counts)) return -2;
                int table = refs + counts[1] * refWidth;
                return DefinitionElement(image, streams, methods, counts, table, row);
            }
            return NamedElement(image, streams, name, ns);
        }

        // Find the nominal CoreLib definition behind a CLI primitive element.
        // The caller can use its InterfaceImpl rows without registering a GC layout.
        internal static int CoreDefinition(byte[] image, int[] streams, int[] methods, int element)
        {
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, streams[4], streams[5], counts);
            if (first < 0) return first;
            if (!PlatformAssemblyIdentity.IsCoreLibDefinition(image, streams, methods, counts)) return 0;
            int strings = methods[2];
            int guid = (image[streams[4] + 6] & 2) == 0 ? 2 : 4;
            int scope = counts[0] < 16384 && counts[26] < 16384 && counts[35] < 16384 && counts[1] < 16384 ? 2 : 4;
            int table = first + counts[0] * (2 + strings + 3 * guid) + counts[1] * (scope + 2 * strings);
            int found = 0;
            for (int row = 1; row <= counts[2]; row++)
            {
                if (DefinitionElement(image, streams, methods, counts, table, row) != element) continue;
                if (found != 0) return -1;
                found = row;
            }
            return found;
        }

        private static int DefinitionElement(byte[] image, int[] streams, int[] methods, int[] counts, int table, int row)
        {
            int strings = methods[2];
            int extends = counts[1] < 16384 && counts[2] < 16384 && counts[27] < 16384 ? 2 : 4;
            int width = 4 + 2 * strings + extends + Width(counts[4]) + Width(counts[6]);
            uint type = (uint)(table + (row - 1) * width);
            if ((U32(image, type) & 7) != 1) return -2; // Top-level public primitive.
            uint name = Index(image, type + 4, strings);
            uint ns = Index(image, type + (uint)(4 + strings), strings);
            return NamedElement(image, streams, name, ns);
        }

        private static int NamedElement(byte[] image, int[] streams, uint name, uint ns)
        {
            uint heapSize = (uint)streams[3];
            if (ns == 0 || name == 0 || !Fits(ns, 7, heapSize) || !Fits(name, 1, heapSize))
                return -1;
            uint baseOffset = (uint)streams[2];
            uint namespaceOffset = baseOffset + ns;
            if (U32(image, namespaceOffset) != 0x74737953 || U16(image, namespaceOffset + 4) != 0x6d65 ||
                image[namespaceOffset + 6] != 0)
                return -2; // System
            if (Named(image, baseOffset, heapSize, name, 7, 0x6c6f6f42, 0x6e6165)) return 2; // Boolean
            if (Named(image, baseOffset, heapSize, name, 4, 0x72616843, 0)) return 3; // Char
            if (Named(image, baseOffset, heapSize, name, 5, 0x74794253, 0x65)) return 4; // SByte
            if (Named(image, baseOffset, heapSize, name, 4, 0x65747942, 0)) return 5; // Byte
            if (Named(image, baseOffset, heapSize, name, 5, 0x31746e49, 0x36)) return 6; // Int16
            if (Named(image, baseOffset, heapSize, name, 6, 0x746e4955, 0x3631)) return 7; // UInt16
            if (Named(image, baseOffset, heapSize, name, 5, 0x33746e49, 0x32)) return 8; // Int32
            if (Named(image, baseOffset, heapSize, name, 6, 0x746e4955, 0x3233)) return 9; // UInt32
            if (Named(image, baseOffset, heapSize, name, 5, 0x36746e49, 0x34)) return 0x0a; // Int64
            if (Named(image, baseOffset, heapSize, name, 6, 0x746e4955, 0x3436)) return 0x0b; // UInt64
            if (Named(image, baseOffset, heapSize, name, 6, 0x676e6953, 0x656c)) return 0x0c; // Single
            if (Named(image, baseOffset, heapSize, name, 6, 0x62756f44, 0x656c)) return 0x0d; // Double
            if (Named(image, baseOffset, heapSize, name, 6, 0x50746e49, 0x7274)) return 0x18; // IntPtr
            if (Named(image, baseOffset, heapSize, name, 7, 0x746e4955, 0x727450)) return 0x19; // UIntPtr
            return -2;
        }

        // The packed words hold the ASCII spelling without a terminator.
        private static bool Named(byte[] image, uint heap, uint size, uint index, int length, uint first, uint tail)
        {
            if (!Fits(index, (uint)length + 1, size)) return false;
            uint at = heap + index;
            if (U32(image, at) != first || image[at + (uint)length] != 0) return false;
            for (int i = 4; i < length; i++)
                if (image[at + (uint)i] != unchecked((byte)(tail >> ((i - 4) * 8)))) return false;
            return true;
        }

        private static int Width(int count) => count < 65536 ? 2 : 4;

        private static uint Index(byte[] image, uint start, int width) =>
            width == 2 ? U16(image, start) : U32(image, start);
    }
}
