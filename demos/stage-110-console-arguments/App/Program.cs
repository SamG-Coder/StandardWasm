using System;

internal static class Program
{
    // The exit status counts the arguments and ten times their UTF-16 code
    // units, so an empty argument, a space and a surrogate pair all change it.
    private static int Main(string[] args)
    {
        string[] reversed = new string[args.Length];
        int units = 0;
        for (int i = 0; i < args.Length; i++)
        {
            reversed[args.Length - 1 - i] = args[i];
            units += args[i].Length;
        }
        for (int i = 0; i < reversed.Length; i++)
        {
            Console.Out.Write("[");
            Console.Out.Write(reversed[i]);
            Console.Out.WriteLine("]");
        }
        string missing = null;
        Console.Out.Write(missing);
        Console.Out.WriteLine(missing);
        Console.Out.WriteLine();
        Console.Error.WriteLine(args.Length == 0 ? "no arguments" : "first: " + args[0]);
        // Flush has no observable effect on either stream (decision 0104).
        Console.Out.Flush();
        Console.Error.Flush();
        return args.Length + 10 * units;
    }
}
