using System;

namespace StringVectors
{
    public static class Kernels
    {
        // newarr String, stelem.ref and ldelem.ref, with null default elements.
        public static int Store(int length, int index)
        {
            var values = new string[length];
            values[index] = "store";
            int nulls = 0;
            for (int i = 0; i < values.Length; i++)
                if (values[i] == null) nulls++;
            return values[index].Length * 100 + nulls;
        }

        // A default element is null; reading its Length raises NullReferenceException.
        public static int ReadDefault(int length, int index)
        {
            var values = new string[length];
            return values[index].Length;
        }

        // Storing null replaces an element. The array keeps its length.
        public static int Clear(int index)
        {
            var values = new string[2];
            values[0] = "a";
            values[1] = "bc";
            values[index] = null;
            return (values[0] == null ? 10 : values[0].Length) + (values[1] == null ? 20 : values[1].Length) + values.Length * 100;
        }

        // ldlen and ldelem.ref on a null vector raise NullReferenceException.
        public static int NullVector(int choice)
        {
            string[] values = choice > 0 ? new string[choice] : null;
            return choice < 0 ? values[0].Length : values.Length;
        }

        // Elements keep reference identity and order across a reversing copy.
        public static int Reverse(int length)
        {
            var source = new string[length];
            for (int i = 0; i < length; i++) source[i] = (i & 1) == 0 ? "even" : "odd";
            var target = new string[source.Length];
            for (int i = 0; i < source.Length; i++) target[source.Length - 1 - i] = source[i];
            int total = 0;
            for (int i = 0; i < target.Length; i++) total = total * 3 + target[i].Length;
            return total;
        }
    }

    // Console members outside the host contract must be diagnosed.
    public static class Unsupported
    {
        public static int StaticWriteLine()
        {
            Console.WriteLine("static");
            return 1;
        }

        // Console.ReadLine is supported since Stage 151 (decision 0079); Console.Read() is not.
        public static int ReadLine() => Console.ReadLine() == null ? 0 : 1;

        public static int Read() => Console.Read();

        public static int Flush()
        {
            Console.Out.Flush();
            return 1;
        }

        public static int WriteCharacter()
        {
            Console.Out.Write('c');
            return 1;
        }

        // The reassigned writer needs a TextWriter local slot.
        public static int WriterLocal(int choice)
        {
            var writer = Console.Error;
            if (choice > 0) writer = Console.Out;
            writer.Write("local");
            writer.WriteLine();
            return choice;
        }

        public static int Supported()
        {
            Console.Out.WriteLine("supported");
            return 2;
        }
    }
}
