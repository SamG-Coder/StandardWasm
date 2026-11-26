using System;

namespace NullAccess
{
    public sealed class Cell
    {
        public Cell Next;
        public int Value;
        public int[] Items;
    }

    public static class Kernels
    {
        public static int ReadArray(int present, int index)
        {
            var cell = new Cell();
            if (present != 0) cell.Items = new int[1];
            try { return GetElement(cell.Items, index); }
            catch (IndexOutOfRangeException) { return -101; }
            catch (NullReferenceException) { return -102; }
        }

        public static int WriteArray(int index)
        {
            var cell = new Cell();
            cell.Items[index] = 5;
            return 0;
        }

        public static int ArrayLength(int present)
        {
            var cell = new Cell();
            if (present != 0) cell.Items = new int[2];
            return cell.Items.Length;
        }

        public static int ReadField(int present)
        {
            var cell = new Cell();
            if (present != 0) cell.Next = new Cell();
            return cell.Next.Value;
        }

        public static int WriteField(int present)
        {
            var cell = new Cell();
            if (present != 0) cell.Next = new Cell();
            try { cell.Next.Value = 7; }
            catch (NullReferenceException) { return -201; }
            return cell.Next.Value;
        }

        public static int Character(int present, int index)
        {
            string text = present != 0 ? "A" : null;
            try
            {
                try { return text[index]; }
                catch (NullReferenceException) { throw; }
            }
            catch (IndexOutOfRangeException) { return -301; }
            catch (NullReferenceException) { return -302; }
        }

        public static int StringLength(int present)
        {
            string text = present != 0 ? "AB" : null;
            return text.Length;
        }

        public static int ConcatNullable(int present)
        {
            string text = present != 0 ? "A" : null;
            return string.Concat(text, "B").Length;
        }

        private static int GetElement(int[] items, int index) => items[index];
    }

    public static class NullDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] data, int index, int value) => data[index] = unchecked((byte)value);
        public static int ReadByte(byte[] data, int index) => data[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int Compile(byte[] image, int row, byte[] output) =>
            PortableCompiler.MethodCompiler.Compile(image, row, output);
    }
}
