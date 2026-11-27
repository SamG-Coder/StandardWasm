using static PeLibrary.ImageBytes;
using static PeLibrary.PlatformTypeReferences;

namespace PeLibrary
{
    internal static class ReflectionMembers
    {
        internal const int TypeModule = 1, TypeAssembly = 2, ModuleAssembly = 3, ModuleName = 4, ResourceStream = 5, ExecutingAssembly = 6, TypeName = 7;

        internal static bool IsCoreTypeAssemblyGetter(byte[] image, int[] heaps, int[] layout, int row)
        {
            if (!MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), "get_Assembly") ||
                (MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x450) != 0x440) return false;

            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts)) return false;
            int owner = ObjectLayouts.Owner(image, heaps, layout, row, false);
            if (owner < 1 || !ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, owner, "System", "Type"))
                return false;

            var cursor = new int[1];
            int end = SignatureReader.ReadInstance(image, heaps, MethodDefinitions.ReadColumn(image, layout, row, 4), 0, cursor);
            if (end < 0 || !SignatureReader.TakeElement(image, cursor, end, 0x12)) return false;
            int result = CompressedUnsigned(image, cursor[0], end, cursor);
            return result > 0 && (result & 3) == 0 && cursor[0] == end &&
                ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, result >> 2, "System.Reflection", "Assembly");
        }

        internal static int Identify(byte[] image, int[] heaps, int[] layout, int row, bool staticCall)
        {
            var member = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, row, member) < 0 ||
                member[AssemblyReferences.Depth] != 0) return 0;
            if (staticCall && !MatchesAscii(image, heaps, (uint)member[4], "GetExecutingAssembly")) return 0;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0) return 0;
            int owner = PlatformTypeReferences.Identify(image, heaps, layout, counts, first, member[AssemblyReferences.Leaf]);
            bool typeName = !staticCall && (owner == Type || owner == TypeBase) &&
                MatchesAscii(image, heaps, (uint)member[4], "get_Name");
            if (owner != Type && owner != Module && owner != Assembly && !typeName) return 0;

            uint heap = (uint)heaps[2], length = (uint)heaps[3], name = (uint)member[4];
            int kind = 0, result = 0;
            if (staticCall)
            {
                if (owner != Assembly) return 0;
                kind = ExecutingAssembly;
                result = Assembly;
            }
            else if (owner == Assembly)
            {
                if (!MatchesAscii(image, heaps, name, "GetManifestResourceStream")) return 0;
                kind = ResourceStream;
                result = Stream;
            }
            else if (owner == Type && Named(image, heap, length, name, 10, 0x5f746567, 0x75646f4d, 0x656c)) // get_Module
            {
                kind = TypeModule;
                result = Module;
            }
            else if (Named(image, heap, length, name, 12, 0x5f746567, 0x65737341, 0x796c626d)) // get_Assembly
            {
                kind = owner == Type ? TypeAssembly : ModuleAssembly;
                result = Assembly;
            }
            else if (typeName)
            {
                kind = TypeName;
                result = String;
            }
            else if (owner == Module && Named(image, heap, length, name, 8, 0x5f746567, 0x656d614e, 0)) // get_Name
            {
                kind = ModuleName;
                result = String;
            }
            if (kind == 0) return 0;

            int blob = member[5], limit = heaps[0] + heaps[1];
            if (blob <= 0 || blob >= heaps[1]) return 0;
            var next = new int[1];
            int size = CompressedUnsigned(image, heaps[0] + blob, limit, next);
            int start = next[0];
            int parameters = kind == ResourceStream ? 1 : 0;
            int convention = staticCall ? 0 : 0x20;
            if (size < 3 || size > limit - start || image[start] != convention || image[start + 1] != parameters) return 0;
            if (result == String) return size == 3 && image[start + 2] == 0x0e ? kind : 0;
            if (image[start + 2] != 0x12) return 0;
            int end = start + size;
            int type = CompressedUnsigned(image, start + 3, end, next);
            int tail = next[0];
            if (end - tail != parameters || (parameters == 1 && image[tail] != 0x0e)) return 0;
            return type > 0 && (type & 3) == 1 &&
                PlatformTypeReferences.Identify(image, heaps, layout, counts, first, type >> 2) == result ? kind : 0;
        }
    }
}
