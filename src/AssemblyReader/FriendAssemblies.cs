using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class FriendAssemblies
    {
        internal static int Allows(byte[] image, int[] heaps, int[] methods, int[] counts,
            byte[] source, int[] sourceHeaps, int[] sourceMethods)
        {
            if (counts[12] == 0 || counts[32] != 1) return 0;
            int declaration = MetadataTableRows.TableStart(heaps, methods, counts, 32);
            int table = MetadataTableRows.TableStart(heaps, methods, counts, 12);
            if (declaration < 0 || table < 0) return -1;
            bool signed = (U32(image, (uint)declaration + 12) & 1) != 0;
            var sourceCounts = new int[45];
            if (MetadataTableRows.ReadCounts(source, sourceHeaps[4], sourceHeaps[5], sourceCounts) < 0 ||
                sourceCounts[32] != 1) return -1;
            int assembly = MetadataTableRows.TableStart(sourceHeaps, sourceMethods, sourceCounts, 32);
            if (assembly < 0) return -1;
            int blobs = sourceMethods[3];
            if (((U32(source, (uint)assembly + 12) & 1) != 0) != signed) return 0;
            var key = new int[1];
            int keyLength = StrongNames.ReadKey(source, sourceHeaps, Index(source, (uint)assembly + 16, blobs), key);
            if (signed ? keyLength <= 0 : keyLength != 0) return -1;
            int name = Index(source, (uint)(assembly + 16 + blobs), sourceMethods[2]);
            if (name < 1 || name >= sourceHeaps[3]) return -1;
            int sourceName = sourceHeaps[2] + name;
            int sourceEnd = sourceHeaps[2] + sourceHeaps[3];
            int parentWidth = StandaloneSignatures.HasCustomAttribute(counts);
            int width = StandaloneSignatures.Width(counts, 12, methods[2], methods[3]);
            int constructorWidth = width - parentWidth - methods[3];
            var attribute = new int[AssemblyReferences.SymbolWidth];
            var value = new int[1];
            for (int i = 0; i < counts[12]; i++)
            {
                uint row = (uint)(table + i * width);
                if (Index(image, row, parentWidth) != (1 << 5 | 14)) continue; // Assembly row 1.
                int constructor = Index(image, row + (uint)parentWidth, constructorWidth);
                if ((constructor & 7) != 3 || !IsGrant(image, heaps, methods, counts, constructor >> 3, attribute))
                    continue;
                int index = Index(image, row + (uint)(parentWidth + constructorWidth), methods[3]);
                int length = StrongNames.ReadKey(image, heaps, index, value);
                if (length < 5 || U16(image, (uint)value[0]) != 1) continue;
                int end = value[0] + length;
                int size = CompressedUnsigned(image, value[0] + 2, end, value);
                int start = value[0];
                if (size <= 0 || size != end - start - 2 || U16(image, (uint)(end - 2)) != 0) continue;
                if (MatchesGrant(image, start, end - 2, source, sourceName, sourceEnd, key[0], keyLength)) return 1;
            }
            return 0;
        }

        private static bool IsGrant(byte[] image, int[] heaps, int[] methods, int[] counts, int ctor, int[] reference)
        {
            if (AssemblyReferences.ReadMember(image, heaps, methods, ctor, reference) != 0 ||
                !PlatformAssemblyIdentity.IsPlatformAssembly(image, heaps, methods, counts, reference[AssemblyReferences.AssemblyRow]))
                return false;
            if (reference[AssemblyReferences.Depth] != 0 || !MatchesAttributeName(image, heaps, reference)) return false;
            int sig = reference[5];
            return sig > 0 && sig <= heaps[1] - 5 && U32(image, (uint)(heaps[0] + sig)) == 0x01012004 &&
                image[heaps[0] + sig + 4] == 0x0e;
        }

        private static bool MatchesAttributeName(byte[] image, int[] heaps, int[] reference)
        {
            uint name = (uint)reference[2], space = (uint)reference[3], member = (uint)reference[4];
            uint size = (uint)heaps[3], start = (uint)heaps[2];
            if (!Fits(name, 28, size) || !Fits(space, 32, size) || !Fits(member, 6, size)) return false;
            uint iv = start + name, ns = start + space, ctor = start + member;
            // InternalsVisibleToAttribute, System.Runtime.CompilerServices, .ctor.
            return U32(image, iv) == 0x65746e49 &&
                U32(image, iv + 4) == 0x6c616e72 &&
                U32(image, iv + 8) == 0x73695673 &&
                U32(image, iv + 12) == 0x656c6269 &&
                U32(image, iv + 16) == 0x74416f54 &&
                U32(image, iv + 20) == 0x62697274 &&
                image[iv + 24] == 'u' &&
                image[iv + 25] == 't' &&
                image[iv + 26] == 'e' &&
                image[iv + 27] == 0 &&
                U32(image, ns) == 0x74737953 &&
                U32(image, ns + 4) == 0x522e6d65 &&
                U32(image, ns + 8) == 0x69746e75 &&
                U32(image, ns + 12) == 0x432e656d &&
                U32(image, ns + 16) == 0x69706d6f &&
                U32(image, ns + 20) == 0x5372656c &&
                U32(image, ns + 24) == 0x69767265 &&
                image[ns + 28] == 'c' &&
                image[ns + 29] == 'e' &&
                image[ns + 30] == 's' &&
                image[ns + 31] == 0 &&
                U32(image, ctor) == 0x6f74632e &&
                image[ctor + 4] == 'r' &&
                image[ctor + 5] == 0;
        }

        private static bool MatchesGrant(byte[] image, int start, int end, byte[] source,
            int name, int nameEnd, int key, int keyLength)
        {
            start = SkipSpaces(image, start, end);
            while (name < nameEnd && source[name] != 0)
            {
                if (start >= end || FoldFriendAscii(image[start]) != FoldFriendAscii(source[name])) return false;
                start++;
                name++;
            }
            if (name == nameEnd) return false;
            start = SkipSpaces(image, start, end);
            if (keyLength == 0) return start == end;
            if (start == end || image[start++] != ',') return false;
            start = SkipSpaces(image, start, end);
            if (end - start < 9 || !FoldedWord(image, start, 0x6c627570) ||
                !FoldedWord(image, start + 4, 0x656b6369) || FoldFriendAscii(image[start + 8]) != 'y') return false; // publickey
            start += 9;
            start = SkipSpaces(image, start, end);
            if (start == end || image[start++] != '=') return false;
            start = SkipSpaces(image, start, end);
            if (keyLength > (end - start) / 2) return false;
            for (int i = 0; i < keyLength; i++)
            {
                int high = Hex(image[start++]), low = Hex(image[start++]);
                if (high < 0 || low < 0 || (high << 4 | low) != source[key + i]) return false;
            }
            return SkipSpaces(image, start, end) == end;
        }

        private static int SkipSpaces(byte[] image, int start, int end)
        {
            while (start < end && (image[start] == ' ' || image[start] == '\t')) start++;
            return start;
        }

        private static int FoldFriendAscii(int value) => value >= 'A' && value <= 'Z' ? value + 32 : value;

        private static bool FoldedWord(byte[] image, int at, uint word)
        {
            for (int byteIndex = 0; byteIndex < 4; byteIndex++)
                if (FoldFriendAscii(image[at + byteIndex]) != (int)((word >> (byteIndex * 8)) & 255)) return false;
            return true;
        }
        private static int Hex(int value)
        {
            if (value >= '0' && value <= '9') return value - '0';
            value = FoldFriendAscii(value);
            return value >= 'a' && value <= 'f' ? value - 'a' + 10 : -1;
        }

        private static int Index(byte[] image, uint at, int width) =>
            width == 2 ? (int)U16(image, at) : unchecked((int)U32(image, at));
    }
}
