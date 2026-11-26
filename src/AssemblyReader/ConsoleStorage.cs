namespace PeLibrary
{
    internal static class ConsoleStorage
    {
        // Evaluation stack type of a TextWriter returned by Console.Out or
        // Console.Error. Wasm holds the host stream number (1 or 2) as i32.
        // The tag exists only on the stack: locals, fields, parameters and
        // arrays of TextWriter still bind through the ordinary linker and
        // are rejected there, so the handle cannot escape a single call.
        internal const byte Writer = PointerStorage.NativeZero + 1;
    }
}
