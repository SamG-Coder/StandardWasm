using System.Text.Json;
if (IntPtr.Size != 4) throw new InvalidOperationException("Use the Windows x86 CLR.");
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    clr = Environment.Version.ToString(),
    results = new { Resolve = Guest.Resolve(), Size = Guest.Size(), SizeWithoutOutput = Guest.SizeWithoutOutput(),
        Short = Guest.Short(), Failure = Guest.Failure(), NativeOutput = Guest.NativeOutput(), SharedArray = Guest.SharedArray() }
}));
