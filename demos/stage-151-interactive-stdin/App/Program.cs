using System;

// Stage 151 (decision 0079): an ordinary console app that prompts, reads lines until EOF,
// and answers each one. The exit status encodes the lines and code units it read, and
// whether it saw an empty line, so an empty line and EOF stay distinguishable.
static class Program
{
    // The prompt interleaves the two streams (stdout "name", stderr "!", stdout "? ") right
    // before a read waits, so an interactive host must keep the order of stream transitions.
    static void Prompt()
    {
        Console.Out.Write("name");
        Console.Error.Write("!");
        Console.Out.Write("? ");
    }

    // "units" mode prints each line's UTF-16 code units as hex, so a decoding difference
    // cannot hide behind the stdout encoding (raw redirected bytes, Codex review of 26fc3a4).
    static string Hex(string line)
    {
        const string digits = "0123456789abcdef";
        var text = new char[line.Length * 5];
        for (int i = 0; i < line.Length; i++)
        {
            int unit = line[i];
            text[i * 5] = ' ';
            text[i * 5 + 1] = digits[(unit >> 12) & 15];
            text[i * 5 + 2] = digits[(unit >> 8) & 15];
            text[i * 5 + 3] = digits[(unit >> 4) & 15];
            text[i * 5 + 4] = digits[unit & 15];
        }
        return new string(text);
    }

    static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "units")
        {
            int count = 0;
            string raw;
            while ((raw = Console.ReadLine()) != null)
            {
                count++;
                Console.Out.Write("units");
                Console.Out.WriteLine(Hex(raw));
            }
            return count;
        }
        int lines = 0, units = 0, empty = 0;
        Prompt();
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
            Prompt();
        }
        Console.Out.WriteLine();
        Console.Error.WriteLine("eof");
        return lines + 10 * empty + 100 * units;
    }
}
