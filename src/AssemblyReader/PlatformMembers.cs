using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    // Identifies calls to platform System.Math methods that decision 0003 maps to exact Wasm instructions. A name is
    // not an identity: any assembly can define System.Math, for example a dependency reached through an extern alias.
    // So a MemberRef qualifies only when:
    //   - its parent is a TypeRef named System.Math,
    //   - that TypeRef's resolution scope is a culture-neutral AssemblyRef whose name and 8-byte public key token are
    //     those of a platform BCL assembly (netstandard, mscorlib, System.Runtime, System.Runtime.Extensions or
    //     System.Private.CoreLib, the same set as Compiler/PlatformTypes.cs),
    //   - and its signature blob is the exact static signature of a mapped overload.
    // References that carry a full public key instead of a token are not recognized, because computing the token
    // would need SHA-1. Compilers emit tokens in AssemblyRef rows.
    // Everything here stays within the portable compiler's own supported subset, so it is itself self-compiled.
    internal static class PlatformMembers
    {
        internal const int SqrtF64 = 1, AbsF64 = 2, AbsF32 = 3, FloorF64 = 4, CeilingF64 = 5, TruncateF64 = 6,
            RoundF64 = 7, MaxF64 = 8, MinF64 = 9, MaxF32 = 10, MinF32 = 11, MaxI32 = 12, MinI32 = 13, MaxU32 = 14,
            MinU32 = 15, MaxI64 = 16, MinI64 = 17, MaxU64 = 18, MinU64 = 19, AbsI32 = 20, AbsI64 = 21;

        // streams: blob start/size, strings start/size, tables start/size (as used by the other readers).
        // methods: MethodDef layout with the string and blob index widths at [2] and [3].
        internal static int MathIntrinsic(byte[] image, int[] streams, int[] methods, int row)
        {
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, streams[4], streams[5], counts);
            if (first < 0 || row < 1 || row > counts[10]) return 0;
            int table = MetadataTableRows.TableStart(streams, methods, counts, 10);
            if (table < 0) return 0;
            int strings = methods[2], blobs = methods[3];
            int width = StandaloneSignatures.Width(counts, 10, strings, blobs);
            int parentWidth = width - strings - blobs;
            uint member = (uint)(table + (row - 1) * width);

            // MemberRefParent tag 1 is TypeRef.
            uint parent = Index(image, member, parentWidth);
            int typeRow = (int)(parent >> 3);
            if ((parent & 7) != 1 || typeRow < 1 || typeRow > counts[1]) return 0;
            int guid = (image[streams[4] + 6] & 2) == 0 ? 2 : 4;
            int scopeWidth = counts[0] < 16384 && counts[1] < 16384 && counts[26] < 16384 && counts[35] < 16384 ? 2 : 4;
            uint type = (uint)(first + counts[0] * (2 + strings + 3 * guid) + (typeRow - 1) * (scopeWidth + 2 * strings));
            if (!Text(image, streams, Index(image, type + (uint)scopeWidth, strings), 4, 0x6874614d, 0, 0, 0, 0, 0, 0) ||
                !Text(image, streams, Index(image, type + (uint)(scopeWidth + strings), strings), 6, 0x74737953, 0x6d65, 0, 0, 0, 0, 0))
                return 0; // Math in namespace System.

            // ResolutionScope tag 2 is AssemblyRef.
            uint scope = Index(image, type, scopeWidth);
            int assemblyRow = (int)(scope >> 2);
            if ((scope & 3) != 2 || assemblyRow < 1 || assemblyRow > counts[35]) return 0;
            int references = MetadataTableRows.TableStart(streams, methods, counts, 35);
            if (references < 0) return 0;
            uint assembly = (uint)(references + (assemblyRow - 1) * (12 + 2 * strings + 2 * blobs));
            if (!IsPlatformAssembly(image, streams, assembly, strings, blobs)) return 0;

            uint name = Index(image, member + (uint)parentWidth, strings);
            uint signature = Index(image, member + (uint)(parentWidth + strings), blobs);
            return Overload(image, streams, name, signature);
        }

        // AssemblyRef row: version (8 bytes), flags (4), PublicKeyOrToken blob, Name string, Culture string, HashValue blob.
        private static bool IsPlatformAssembly(byte[] image, int[] streams, uint assembly, int strings, int blobs)
        {
            if ((U32(image, assembly + 8) & 1) != 0) return false; // full public key, not a token
            uint key = Index(image, assembly + 12, blobs);
            if (!Fits(key, 9, (uint)streams[1])) return false;
            uint token = (uint)streams[0] + key;
            if (image[token] != 8) return false; // compressed length of an 8-byte token
            uint low = U32(image, token + 1), high = U32(image, token + 5);
            uint name = Index(image, assembly + (uint)(12 + blobs), strings);
            // The platform assemblies are culture-neutral. A reference with the same name and token but a culture
            // such as "fr" names a different (satellite) assembly that the CLR fails to bind, so it is not the BCL.
            uint culture = Index(image, assembly + (uint)(12 + blobs + strings), strings);
            if (!Text(image, streams, culture, 0, 0, 0, 0, 0, 0, 0, 0)) return false;
            if (low == 0xff137bcc && high == 0x51dd2dcd) // cc7b13ffcd2ddd51
                return Text(image, streams, name, 11, 0x7374656e, 0x646e6174, 0x647261, 0, 0, 0, 0); // netstandard
            if (low == 0x565c7ab7 && high == 0x89e03419) // b77a5c561934e089
                return Text(image, streams, name, 8, 0x6f63736d, 0x62696c72, 0, 0, 0, 0, 0); // mscorlib
            if (low == 0x7f5f3fb0 && high == 0x3a0ad511) // b03f5f7f11d50a3a
                return Text(image, streams, name, 14, 0x74737953, 0x522e6d65, 0x69746e75, 0x656d, 0, 0, 0) || // System.Runtime
                    Text(image, streams, name, 25, 0x74737953, 0x522e6d65, 0x69746e75, 0x452e656d, 0x6e657478,
                        0x6e6f6973, 0x73); // System.Runtime.Extensions
            if (low == 0xd785ec7c && high == 0x8e79a7be) // 7cec85d7bea7798e
                return Text(image, streams, name, 22, 0x74737953, 0x502e6d65, 0x61766972, 0x432e6574, 0x4c65726f,
                    0x6269, 0); // System.Private.CoreLib
            return false;
        }

        // Method name plus exact MethodRefSig: DEFAULT (static), parameter count, return type, parameter types.
        // Element types: R8 0x0d, R4 0x0c, I4 0x08, U4 0x09, I8 0x0a, U8 0x0b.
        private static int Overload(byte[] image, int[] streams, uint name, uint signature)
        {
            if (!Fits(signature, 6, (uint)streams[1])) return 0;
            uint blob = (uint)streams[0] + signature;
            int size = image[blob];
            if (size < 4 || size > 5 || image[blob + 1] != 0) return 0;
            if (!Fits(signature, (uint)size + 1, (uint)streams[1])) return 0;
            int count = image[blob + 2];
            if (size != count + 3) return 0;
            int result = image[blob + 3];
            for (int i = 0; i < count; i++)
                if (image[blob + 4 + (uint)i] != result) return 0; // every mapped overload is T(T) or T(T, T)

            if (count == 1)
            {
                if (Text(image, streams, name, 4, 0x74727153, 0, 0, 0, 0, 0, 0)) return result == 0x0d ? SqrtF64 : 0;
                if (Text(image, streams, name, 3, 0x736241, 0, 0, 0, 0, 0, 0))
                    return result == 0x0d ? AbsF64 : result == 0x0c ? AbsF32 : result == 0x08 ? AbsI32 :
                        result == 0x0a ? AbsI64 : 0;
                if (result != 0x0d) return 0;
                if (Text(image, streams, name, 5, 0x6f6f6c46, 0x72, 0, 0, 0, 0, 0)) return FloorF64;
                if (Text(image, streams, name, 7, 0x6c696543, 0x676e69, 0, 0, 0, 0, 0)) return CeilingF64;
                if (Text(image, streams, name, 8, 0x6e757254, 0x65746163, 0, 0, 0, 0, 0)) return TruncateF64;
                if (Text(image, streams, name, 5, 0x6e756f52, 0x64, 0, 0, 0, 0, 0)) return RoundF64;
                return 0;
            }
            bool max = Text(image, streams, name, 3, 0x78614d, 0, 0, 0, 0, 0, 0);
            if (!max && !Text(image, streams, name, 3, 0x6e694d, 0, 0, 0, 0, 0, 0)) return 0;
            if (result == 0x0d) return max ? MaxF64 : MinF64;
            if (result == 0x0c) return max ? MaxF32 : MinF32;
            if (result == 0x08) return max ? MaxI32 : MinI32;
            if (result == 0x09) return max ? MaxU32 : MinU32;
            if (result == 0x0a) return max ? MaxI64 : MinI64;
            if (result == 0x0b) return max ? MaxU64 : MinU64;
            return 0;
        }

        // True when the #Strings entry at index is exactly the given ASCII text followed by its terminator. The text is
        // passed as up to seven little-endian words so the comparison needs no string support.
        private static bool Text(byte[] image, int[] streams, uint index, int length, uint w0, uint w1, uint w2,
            uint w3, uint w4, uint w5, uint w6)
        {
            if (!Fits(index, (uint)length + 1, (uint)streams[3])) return false;
            uint at = (uint)streams[2] + index;
            for (int i = 0; i < length; i++)
            {
                int word = i >> 2;
                uint packed = word == 0 ? w0 : word == 1 ? w1 : word == 2 ? w2 : word == 3 ? w3 : word == 4 ? w4 :
                    word == 5 ? w5 : w6;
                if (image[at + (uint)i] != ((packed >> ((i & 3) * 8)) & 0xff)) return false;
            }
            return image[at + (uint)length] == 0;
        }

        private static uint Index(byte[] image, uint at, int width) => width == 2 ? U16(image, at) : U32(image, at);
    }
}
