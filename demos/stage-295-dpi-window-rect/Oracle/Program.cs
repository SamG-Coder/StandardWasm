using System.Text.Json;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
int[] dpis = [96, 97, 110, 120, 132, 144, 168, 192, 216, 240, 288];
int[] styles = [unchecked((int)0x80000000), 0x40000000, 0x00400000, 0x00800000, 0x00040000, 0x00C00000,
    0x00C80000, 0x00CF0000, unchecked((int)0x80C80000)];
int[] extendedStyles = [0, 1, 0x80, 0x100, 0x200, 0x40000, 0x20000, 0x20001, 0x201, 0x280];
var cases = (from dpi in dpis from style in styles from extendedStyle in extendedStyles
             from menu in new[] {0, 1} select Probe(style, extendedStyle, menu, dpi, [10, 20, 310, 220])).ToList();
foreach (int dpi in Enumerable.Range(0, 69))
    cases.Add(Probe(0x00CF0000, 0x200, -1, dpi, [-12, -25, 88, 211]));
cases.Add(Probe(0x00C80000, 0, 0, 192, [60, 70, -22, -51]));
File.WriteAllText(args[0], JsonSerializer.Serialize(new {
    assembly = typeof(Guest).Assembly.Location, runtime = Environment.Version.ToString(), cases
}));

static unsafe object Probe(int style, int extendedStyle, int menu, int dpi, int[] before)
{
    int* rectangle = stackalloc int[4];
    for (int i = 0; i < 4; i++) rectangle[i] = before[i];
    int result = Guest.Adjust((nint)rectangle, style, menu, extendedStyle, dpi);
    return new { style, extendedStyle, menu, dpi, before, result,
        after = new[] { rectangle[0], rectangle[1], rectangle[2], rectangle[3] } };
}
