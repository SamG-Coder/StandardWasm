using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class PlatformAssemblyIdentity
    {
        // Validate the exact AssemblyRef name, token and neutral culture.
        internal static bool IsPlatformAssembly(byte[] image, int[] heaps, int[] methods, int[] counts, int row,
            bool netstandardOnly = false, bool includeRuntimeExtensions = true, bool includeRuntime = true, bool includeMemory = false)
        {
            int table = MetadataTableRows.TableStart(heaps, methods, counts, 35);
            if (table < 0 || row < 1 || row > counts[35]) return false;
            int strings = methods[2], blobs = methods[3];
            uint at = (uint)(table + (row - 1) * (12 + 2 * strings + 2 * blobs));
            if (U32(image, at + 8) != 0 || Index(image, at + (uint)(12 + blobs + strings), strings) != 0) return false;
            uint key = Index(image, at + 12, blobs);
            if (key == 0 || !Fits(key, 9, (uint)heaps[1])) return false;
            uint blob = (uint)heaps[0] + key;
            if (image[blob] != 8) return false;
            uint low = U32(image, blob + 1), high = U32(image, blob + 5);
            uint name = Index(image, at + (uint)(12 + blobs), strings);
            uint n = (uint)heaps[2] + name, size = (uint)heaps[3];
            if (name == 0) return false;
            if (low == 0xff137bcc && high == 0x51dd2dcd) // netstandard, System.Memory
                return MatchesAscii(image, heaps, name, "netstandard") ||
                    includeMemory && !netstandardOnly && MatchesAscii(image, heaps, name, "System.Memory");
            if (netstandardOnly) return false;
            if (low == 0x565c7ab7 && high == 0x89e03419) // mscorlib
                return Fits(name, 9, size) && U32(image, n) == 0x6f63736d &&
                    U32(image, n + 4) == 0x62696c72 && image[n + 8] == 0;
            if (low == 0x7f5f3fb0 && high == 0x3a0ad511) // System.Runtime, System.Runtime.Extensions
            {
                if (!Fits(name, 15, size) || U32(image, n) != 0x74737953 ||
                    U32(image, n + 4) != 0x522e6d65 || U32(image, n + 8) != 0x69746e75)
                    return false;
                return includeRuntime && U16(image, n + 12) == 0x656d && image[n + 14] == 0 ||
                    includeRuntimeExtensions && Fits(name, 26, size) &&
                    U32(image, n + 12) == 0x452e656d && U32(image, n + 16) == 0x6e657478 &&
                    U32(image, n + 20) == 0x6e6f6973 && U16(image, n + 24) == 0x73;
            }
            // System.Private.CoreLib
            return low == 0xd785ec7c && high == 0x8e79a7be && IsCoreLibName(image, n, name, size);
        }

        // System.Console is a separate reference assembly in .NET Core. .NET
        // Standard and .NET Framework libraries reach it through their facade.
        internal static bool IsConsoleAssembly(byte[] image, int[] heaps, int[] methods, int[] counts, int row)
        {
            if (IsPlatformAssembly(image, heaps, methods, counts, row, netstandardOnly: true)) return true;
            int table = MetadataTableRows.TableStart(heaps, methods, counts, 35);
            if (table < 0 || row < 1 || row > counts[35]) return false;
            int strings = methods[2], blobs = methods[3];
            uint at = (uint)(table + (row - 1) * (12 + 2 * strings + 2 * blobs));
            if (U32(image, at + 8) != 0 || Index(image, at + (uint)(12 + blobs + strings), strings) != 0) return false;
            uint key = Index(image, at + 12, blobs);
            if (key == 0 || !Fits(key, 9, (uint)heaps[1])) return false;
            uint blob = (uint)heaps[0] + key;
            if (image[blob] != 8) return false;
            uint low = U32(image, blob + 1), high = U32(image, blob + 5);
            uint name = Index(image, at + (uint)(12 + blobs), strings);
            uint n = (uint)heaps[2] + name, size = (uint)heaps[3];
            if (name == 0) return false;
            if (low == 0x565c7ab7 && high == 0x89e03419) // mscorlib
                return Fits(name, 9, size) && U32(image, n) == 0x6f63736d &&
                    U32(image, n + 4) == 0x62696c72 && image[n + 8] == 0;
            // System.Console carries the same public key token as System.Runtime.
            return low == 0x7f5f3fb0 && high == 0x3a0ad511 && Fits(name, 15, size) &&
                U32(image, n) == 0x74737953 && U32(image, n + 4) == 0x432e6d65 &&
                U32(image, n + 8) == 0x6f736e6f && U16(image, n + 12) == 0x656c && image[n + 14] == 0;
        }

        // Interlocked uses System.Threading on modern .NET. The older
        // netstandard/mscorlib facades and CoreLib are also valid scopes.
        internal static bool IsThreadingAssembly(byte[] image, int[] heaps, int[] methods, int[] counts, int row)
        {
            if (IsPlatformAssembly(image, heaps, methods, counts, row,
                includeRuntimeExtensions: false, includeRuntime: false)) return true;
            uint name = MicrosoftFacadeName(image, heaps, methods, counts, row);
            uint n = (uint)heaps[2] + name, size = (uint)heaps[3];
            return name != 0 && Fits(name, 17, size) && U32(image, n) == 0x74737953 &&
                U32(image, n + 4) == 0x542e6d65 && U32(image, n + 8) == 0x61657268 &&
                U32(image, n + 12) == 0x676e6964 && image[n + 16] == 0; // System.Threading
        }

        // Reflection base types are forwarded by System.Reflection as well as
        // the core facades. System.Runtime.Extensions does not forward them.
        internal static bool IsReflectionAssembly(byte[] image, int[] heaps, int[] methods, int[] counts, int row)
        {
            if (IsPlatformAssembly(image, heaps, methods, counts, row, includeRuntimeExtensions: false)) return true;
            uint name = MicrosoftFacadeName(image, heaps, methods, counts, row);
            return PlatformTypeReferences.Named17(image, (uint)heaps[2], (uint)heaps[3], name,
                0x74737953, 0x522e6d65, 0x656c6665, 0x6f697463, 0x6e); // System.Reflection
        }

        internal static bool IsMarshalAssembly(byte[] image, int[] heaps, int[] methods, int[] counts, int row) =>
            IsPlatformAssembly(image, heaps, methods, counts, row, includeRuntimeExtensions: false) ||
            MatchesAscii(image, heaps, MicrosoftFacadeName(image, heaps, methods, counts, row), "System.Runtime.InteropServices");

        internal static bool IsIntrinsicsAssembly(byte[] image, int[] heaps, int[] methods, int[] counts, int row) =>
            MatchesAscii(image, heaps, MicrosoftFacadeName(image, heaps, methods, counts, row, 0xff137bcc, 0x51dd2dcd),
                "System.Runtime.Intrinsics") ||
            MatchesAscii(image, heaps, MicrosoftFacadeName(image, heaps, methods, counts, row, 0xd785ec7c, 0x8e79a7be),
                "System.Private.CoreLib");

        private static uint MicrosoftFacadeName(byte[] image, int[] heaps, int[] methods, int[] counts, int row,
            uint tokenLow = 0x7f5f3fb0, uint tokenHigh = 0x3a0ad511)
        {
            int table = MetadataTableRows.TableStart(heaps, methods, counts, 35);
            if (table < 0 || row < 1 || row > counts[35]) return 0;
            int strings = methods[2], blobs = methods[3];
            uint at = (uint)(table + (row - 1) * (12 + 2 * strings + 2 * blobs));
            if (U32(image, at + 8) != 0 || Index(image, at + (uint)(12 + blobs + strings), strings) != 0) return 0;
            uint key = Index(image, at + 12, blobs);
            if (key == 0 || !Fits(key, 9, (uint)heaps[1])) return 0;
            uint blob = (uint)heaps[0] + key;
            if (image[blob] != 8 || U32(image, blob + 1) != tokenLow || U32(image, blob + 5) != tokenHigh) return 0;
            return Index(image, at + (uint)(12 + blobs), strings);
        }

        internal static bool IsCoreLibDefinition(byte[] image, int[] heaps, int[] methods, int[] counts)
        {
            if (counts[32] != 1) return false;
            int table = MetadataTableRows.TableStart(heaps, methods, counts, 32);
            if (table < 0) return false;
            int strings = methods[2], blobs = methods[3];
            uint at = (uint)table;
            uint name = Index(image, at + (uint)(16 + blobs), strings);
            if (U32(image, at + 12) != 1 || Index(image, at + (uint)(16 + blobs + strings), strings) != 0 ||
                !IsCoreLibName(image, (uint)heaps[2] + name, name, (uint)heaps[3]))
                return false;
            var key = new int[1];
            int length = StrongNames.ReadKey(image, heaps, (int)Index(image, at + 16, blobs), key);
            return length > 0 && StrongNames.MatchesTokenWords(image, key[0], length, 0xd785ec7c, 0x8e79a7be);
        }

        // Identity matching does not authenticate a framework pack. The host
        // must choose the framework inputs before the compiler resolves them.
        private static bool IsCoreLibName(byte[] image, uint at, uint name, uint limit) =>
            name != 0 && Fits(name, 23, limit) &&
            U32(image, at) == 0x74737953 && U32(image, at + 4) == 0x502e6d65 &&
            U32(image, at + 8) == 0x61766972 && U32(image, at + 12) == 0x432e6574 &&
            U32(image, at + 16) == 0x4c65726f && U16(image, at + 20) == 0x6269 && image[at + 22] == 0;

        private static uint Index(byte[] image, uint at, int width) => width == 2 ? U16(image, at) : U32(image, at);
    }
}
