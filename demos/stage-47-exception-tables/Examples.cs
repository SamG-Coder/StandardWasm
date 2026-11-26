using System;
using PeLibrary;

namespace ExceptionTables
{
    public static class Examples
    {
        public static int SmallCatch(int value)
        {
            try { return checked(value + 1); }
            catch (OverflowException) { return -1; }
        }

        public static int TwoCatches(int value)
        {
            try { return checked(50 / value + int.MaxValue); }
            catch (DivideByZeroException) { return -1; }
            catch (OverflowException) { return -2; }
        }

        public static int FilterAndFinally(int value, int accept)
        {
            try { return 48 / value; }
            catch (DivideByZeroException) when (accept != 0) { return -1; }
            finally { accept = value; }
        }

        public static int FatCatch(int value)
        {
            try
            {
                // A dense switch keeps the protected IL longer than a small
                // section's one-byte length can encode.
                switch (value)
                {
                    case 0: return value + 31;
                    case 1: return value + 62;
                    case 2: return value + 93;
                    case 3: return value + 124;
                    case 4: return value + 155;
                    case 5: return value + 186;
                    case 6: return value + 217;
                    case 7: return value + 248;
                    case 8: return value + 279;
                    case 9: return value + 310;
                    case 10: return value + 341;
                    case 11: return value + 372;
                    case 12: return value + 403;
                    case 13: return value + 434;
                    case 14: return value + 465;
                    case 15: return value + 496;
                    case 16: return value + 527;
                    case 17: return value + 558;
                    case 18: return value + 589;
                    case 19: return value + 620;
                    case 20: return value + 651;
                    case 21: return value + 682;
                    case 22: return value + 713;
                    case 23: return value + 744;
                    case 24: return value + 775;
                    case 25: return value + 806;
                    case 26: return value + 837;
                    case 27: return value + 868;
                    case 28: return value + 899;
                    case 29: return value + 930;
                    case 30: return value + 961;
                    case 31: return value + 992;
                    case 32: return value + 1023;
                    case 33: return value + 1054;
                    case 34: return value + 1085;
                    case 35: return value + 1116;
                    case 36: return value + 1147;
                    case 37: return value + 1178;
                    case 38: return value + 1209;
                    case 39: return value + 1240;
                    case 40: return value + 1271;
                    case 41: return value + 1302;
                    default: return checked(value + 1);
                }
            }
            catch (OverflowException) { return -1; }
        }

        public static int WithoutClauses(int value) => value + 1;
    }

    public static class ExceptionDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] image, int index, int value) => image[index] = unchecked((byte)value);

        public static int InspectNamed(byte[] image, byte[] method) =>
            InspectRow(image, MethodNames.FindRow(image, method), -1, 0);

        public static int InspectRow(byte[] image, int row, int clause, int field)
        {
            var methods = new int[4];
            int count = CompressedTables.Find(image, methods);
            if (count < 0) return count;
            if (row < 1 || row > count) return ExceptionRegions.Malformed;
            uint rva = unchecked((uint)MethodDefinitions.ReadColumn(image, methods, row, 0));
            return InspectRva(image, rva, clause, field);
        }

        public static int InspectRva(byte[] image, uint rva, int clause, int field)
        {
            var body = new int[6];
            int status = MethodBodies.Read(image, rva, body);
            if (status != 0) return status;
            int count = ExceptionRegions.Count(image, rva, body);
            if (count < 0 || clause == -1) return count;
            if (clause < 0 || clause >= count || field < 0 || field >= ExceptionRegions.Width)
                return ExceptionRegions.Malformed;
            var regions = new int[count * ExceptionRegions.Width];
            status = ExceptionRegions.Read(image, rva, body, regions);
            return status < 0 ? status : regions[clause * ExceptionRegions.Width + field];
        }

        public static int ProbeWrites(byte[] image, uint rva, int capacity)
        {
            var body = new int[6];
            int status = MethodBodies.Read(image, rva, body);
            if (status != 0) return status;
            var regions = new int[capacity];
            for (int i = 0; i < regions.Length; i++) regions[i] = 0x55aa55aa;
            status = ExceptionRegions.Read(image, rva, body, regions);
            if (status < 0)
                for (int i = 0; i < regions.Length; i++)
                    if (regions[i] != 0x55aa55aa) return -4;
            return status;
        }
    }
}
