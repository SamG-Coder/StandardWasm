using System;

// Stage 151 (decision 0079): an ordinary console app that prompts, reads lines until EOF,
// and answers each one. The exit status encodes the lines and code units it read, and
// whether it saw an empty line, so an empty line and EOF stay distinguishable.
static class Program
{
    static int Main(string[] args)
    {
        int lines = 0, units = 0, empty = 0;
        Console.Out.Write("name? ");
        string line;
        while ((line = Console.ReadLine()) != null)
        {
            lines++;
            units += line.Length;
            if (line.Length == 0)
            {
                empty++;
                Console.Out.WriteLine("(empty)");
            }
            else
            {
                Console.Out.Write("hello ");
                Console.Out.WriteLine(line);
            }
            Console.Out.Write("name? ");
        }
        Console.Out.WriteLine();
        Console.Error.WriteLine("eof");
        return lines + 10 * empty + 100 * units;
    }
}
