using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    // An oversized sequential leaf is a native fixed buffer only when its
    // actual enclosing field declares that exact primitive and element count.
    internal static class FixedBufferMetadata
    {
        internal static int ManagedLength(byte[] image, int[] layout, int[] heaps, int[] counts,
            int first, int row, int fieldTable, int fieldRow)
        {
            var element = new int[2];
            int bytes = Read(image, layout, heaps, counts, first, row, fieldTable, fieldRow, element);
            return bytes < 0 ? bytes : bytes / element[1];
        }

        // The result also identifies the primitive and alignment for the native visitor.
        internal static int Read(byte[] image, int[] layout, int[] heaps, int[] counts,
            int first, int row, int fieldTable, int fieldRow, int[] element)
        {
            var field = new int[3];
            AssemblyReferences.ReadField(image, layout, fieldTable, fieldRow, field);
            int blob = field[2];
            if ((field[0] & 0x10) != 0 || blob <= 0 || !Fits((uint)blob, 3, (uint)heaps[1])) return -2;
            int at = heaps[0] + blob;
            if (image[at] != 2 || image[at + 1] != 6) return -2;
            int primitive = image[at + 2];
            int width = primitive == 3 || primitive == 6 || primitive == 7 ? 2 :
                primitive == 8 || primitive == 9 || primitive == 12 ? 4 :
                primitive == 10 || primitive == 11 || primitive == 13 ? 8 : 1;
            element[0] = primitive;
            element[1] = width;
            return Extent(image, layout, heaps, counts, first, row, primitive, width);
        }

        private static int Extent(byte[] image, int[] layout, int[] heaps, int[] counts,
            int first, int row, int element, int elementSize)
        {
            string name = PrimitiveName(element);
            if (name == null || elementSize <= 0) return -2;
            var declared = new uint[2];
            int status = ExplicitValueLayouts.ReadClassLayout(image, heaps, layout, counts, row, declared);
            if (status < 0) return status;
            if (declared[0] != 0 || declared[1] == 0 || declared[1] > int.MaxValue) return -2;

            int enclosing = NestedTypes.Enclosing(image, heaps, layout, counts, row);
            if (enclosing <= 0) return enclosing < 0 ? enclosing : -2;
            var parent = new int[5];
            // The enclosing type must have an independently valid field table.
            // Disable the fixed-buffer fallback to avoid recursive type proofs.
            if (ObjectLayouts.ReadShape(image, heaps, layout, enclosing, parent, true, false) != 0) return -2;
            var member = new int[3];
            int limit = heaps[0] + heaps[1];
            var cursor = new int[1];
            int matched = 0;
            int endField = parent[1] + ObjectLayouts.DeclaredShapeFieldCount(parent);
            for (int outer = parent[1]; outer < endField; outer++)
            {
                AssemblyReferences.ReadField(image, layout, parent[0], outer, member);
                int blob = member[2];
                if ((member[0] & 0x10) != 0 || blob <= 0 || blob >= heaps[1]) continue;
                int length = CompressedUnsigned(image, heaps[0] + blob, limit, cursor);
                int at = cursor[0];
                if (length < 3 || length > limit - at || image[at] != 6 || image[at + 1] != 0x11 ||
                    CompressedUnsigned(image, at + 2, at + length, cursor) != row << 2 || cursor[0] != at + length)
                    continue;
                int count = ReadCount(image, layout, heaps, counts, first, outer, name);
                if (count < 0) return count;
                if (count == 0 || matched != 0) return -2;
                matched = count;
            }

            return matched > 0 && matched <= int.MaxValue / elementSize &&
                (uint)(matched * elementSize) == declared[1] ? (int)declared[1] : -2;
        }

        private static int ReadCount(byte[] image, int[] layout, int[] heaps, int[] counts,
            int first, int field, string element)
        {
            if (counts[12] == 0) return 0;
            int table = MetadataTableRows.TableStart(heaps, layout, counts, 12);
            if (table < 0) return -1;
            int parentWidth = StandaloneSignatures.HasCustomAttribute(counts);
            int blobWidth = layout[3];
            int width = StandaloneSignatures.Width(counts, 12, layout[2], blobWidth);
            int ctorWidth = width - parentWidth - blobWidth;
            int count = 0;
            for (int index = 0; index < counts[12]; index++)
            {
                uint at = (uint)(table + index * width);
                if (AttributeConstructors.ReadRowIndex(image, at, parentWidth) != (field << 5 | 1)) continue;
                int ctor = AttributeConstructors.ReadRowIndex(image, at + (uint)parentWidth, ctorWidth);
                int owner = AttributeConstructors.ReadType(image, heaps, layout, counts, ctor);
                if (owner < 0) return owner;
                if (!IsTrustedConstructor(image, layout, heaps, counts, first, ctor, owner)) continue;
                if (count != 0) return -1;
                int payload = AttributeConstructors.ReadRowIndex(image, at + (uint)(width - blobWidth), blobWidth);
                count = ReadPayload(image, heaps, payload, element);
                if (count < 1) return count < 0 ? count : -2;
            }
            return count;
        }

        private static bool IsTrustedConstructor(byte[] image, int[] layout, int[] heaps, int[] counts,
            int first, int ctor, int owner)
        {
            if (owner == 0) return false;
            int signature, name;
            if ((owner & 7) == 0)
            {
                if (!PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts) ||
                    !ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, owner >> 3,
                        "System.Runtime.CompilerServices", "FixedBufferAttribute")) return false;
                signature = MethodDefinitions.ReadColumn(image, layout, ctor >> 3, 4);
                name = MethodDefinitions.ReadColumn(image, layout, ctor >> 3, 3);
            }
            else if ((owner & 7) == 1)
            {
                var member = new int[AssemblyReferences.SymbolWidth];
                if (AssemblyReferences.ReadMember(image, heaps, layout, ctor >> 3, member) < 0 ||
                    member[AssemblyReferences.Depth] != 0 ||
                    !PlatformAssemblyIdentity.IsPlatformAssembly(image, heaps, layout, counts,
                        member[AssemblyReferences.AssemblyRow], includeRuntimeExtensions: false) ||
                    !MatchesAscii(image, heaps, (uint)member[2], "FixedBufferAttribute") ||
                    !MatchesAscii(image, heaps, (uint)member[3], "System.Runtime.CompilerServices")) return false;
                name = member[4];
                signature = member[5];
            }
            else return false;

            var cursor = new int[1];
            int end = SignatureReader.ReadInstance(image, heaps, signature, 2, cursor);
            if (end < 0 || !MatchesAscii(image, heaps, (uint)name, ".ctor") ||
                !SignatureReader.TakeElement(image, cursor, end, SignatureReader.Void)) return false;
            int type = SignatureReader.ReadClass(image, cursor, end);
            if (type <= 0) return false;
            bool systemType = (type & 3) == 0
                ? type >> 2 <= counts[2] && PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts) &&
                    ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, type >> 2, "System", "Type")
                : RuntimeTypeMetadata.Storage(image, heaps, layout, type, false) == RuntimeTypeMetadata.Type;
            return systemType && SignatureReader.TakeElement(image, cursor, end, SignatureReader.Int32) && cursor[0] == end;
        }

        private static int ReadPayload(byte[] image, int[] heaps, int payload, string element)
        {
            if (payload <= 0 || payload >= heaps[1]) return -1;
            var cursor = new int[1];
            int limit = heaps[0] + heaps[1];
            int size = CompressedUnsigned(image, heaps[0] + payload, limit, cursor);
            int at = cursor[0];
            if (size < 9 || size > limit - at || U16(image, (uint)at) != 1) return -1;
            int end = at + size;
            int length = CompressedUnsigned(image, at + 2, end, cursor);
            int start = cursor[0];
            if (length < element.Length || length > end - start - 6) return -2;
            int tail = start + length;
            if (!Matches(image, start, tail, element) || length != element.Length &&
                !IsQualifiedCorePrimitive(image, start + element.Length, tail)) return -2;
            if (end - tail != 6 || U16(image, (uint)tail + 4) != 0) return -2;
            return unchecked((int)U32(image, (uint)tail));
        }

        private static bool IsQualifiedCorePrimitive(byte[] image, int cursor, int end)
        {
            if (!Matches(image, cursor, end, ", ")) return false;
            cursor += 2;
            string token;
            if (Matches(image, cursor, end, "netstandard, "))
            {
                cursor += 13;
                token = "cc7b13ffcd2ddd51";
            }
            else if (Matches(image, cursor, end, "mscorlib, "))
            {
                cursor += 10;
                token = "b77a5c561934e089";
            }
            else if (Matches(image, cursor, end, "System.Runtime, "))
            {
                cursor += 16;
                token = "b03f5f7f11d50a3a";
            }
            else if (Matches(image, cursor, end, "System.Private.CoreLib, "))
            {
                cursor += 24;
                token = "7cec85d7bea7798e";
            }
            else return false;

            if (!Matches(image, cursor, end, "Version=")) return false;
            cursor += 8;
            for (int part = 0; part < 4; part++)
            {
                if (part != 0 && (cursor >= end || image[cursor++] != '.')) return false;
                int start = cursor, value = 0;
                while (cursor < end && image[cursor] >= '0' && image[cursor] <= '9')
                {
                    value = value * 10 + image[cursor++] - '0';
                    if (value > 65535 || cursor - start > 5) return false;
                }
                if (cursor == start) return false;
            }

            const string identity = ", Culture=neutral, PublicKeyToken=";
            return Matches(image, cursor, end, identity) &&
                end - cursor == identity.Length + token.Length && Matches(image, cursor + identity.Length, end, token);
        }

        private static bool Matches(byte[] image, int start, int end, string value)
        {
            if (value.Length > end - start) return false;
            for (int index = 0; index < value.Length; index++)
                if (image[start + index] != value[index]) return false;
            return true;
        }

        private static string PrimitiveName(int element)
        {
            if (element == 2) return "System.Boolean";
            if (element == 3) return "System.Char";
            if (element == 4) return "System.SByte";
            if (element == 5) return "System.Byte";
            if (element == 6) return "System.Int16";
            if (element == 7) return "System.UInt16";
            if (element == 8) return "System.Int32";
            if (element == 9) return "System.UInt32";
            if (element == 10) return "System.Int64";
            if (element == 11) return "System.UInt64";
            if (element == 12) return "System.Single";
            return element == 13 ? "System.Double" : null;
        }
    }
}
