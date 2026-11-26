using static PortableCompiler.BranchIl;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class FloatWasm
    {
        internal static byte Target(int instruction) =>
            instruction == 0x6b || instruction == UnsignedSingle ? F32 : F64;

        internal static int Literal(byte[] code, int used, int type, int low, int high)
        {
            // Wasm literals use the same little-endian IEEE bits as CLI literals.
            code[used++] = type == F32 ? (byte)0x43 : (byte)0x44;
            for (int i = 0; i < 32; i += 8) code[used++] = unchecked((byte)(low >> i));
            if (type == F64)
                for (int i = 0; i < 32; i += 8) code[used++] = unchecked((byte)(high >> i));
            return used;
        }

        internal static int Convert(byte[] code, int used, int instruction, int source, int target)
        {
            if (source == target) return used;
            if (IsFloat(source) && IsFloat(target))
                code[used++] = target == F64 ? (byte)0xbb : (byte)0xb6;
            else if (IsFloat(target))
            {
                int first = target == F32 ? 0xb2 : 0xb7;
                int unsigned = instruction == 0x76 || instruction == UnsignedSingle ? 1 : 0;
                code[used++] = (byte)(first + (source == I64 ? 2 : 0) + unsigned);
            }
            else
            {
                // The .NET 10 CLR truncates, saturates and maps NaN to zero.
                int unsigned = instruction == 0x6d || instruction == 0x6e ? 1 : 0;
                code[used++] = 0xfc;
                code[used++] = (byte)((target == I64 ? 4 : 0) + (source == F64 ? 2 : 0) + unsigned);
            }
            return used;
        }

        internal static int Binary(byte[] code, int used, int instruction, int type)
        {
            if (!IsComparison(instruction))
            {
                code[used++] = (byte)(instruction + (type == F32 ? 0x28 : 0x36));
                return used;
            }

            // CLI .un relations also match unordered inputs. Negate the
            // complementary ordered relation so that NaN takes the same path.
            bool unordered = instruction == 0x49 || instruction == 0x4b ||
                instruction == 0x4d || instruction == 0x4f;
            int comparison;
            if (instruction == 0x46) comparison = 0x5b;
            else if (instruction == 0x47) comparison = 0x5c;
            else if (instruction == 0x48 || instruction == 0x4f) comparison = 0x5d;
            else if (instruction == 0x4a || instruction == 0x4d) comparison = 0x5e;
            else if (instruction == 0x4c || instruction == 0x4b) comparison = 0x5f;
            else comparison = 0x60;
            code[used++] = (byte)(comparison + (type == F32 ? 0 : 6));
            if (unordered) code[used++] = 0x45; // i32.eqz
            return used;
        }
    }
}
