using System.Text.Json;

internal static unsafe class Program
{
    private const int SystemSeed = 0x1234, SavedSeed = 0x5678;
    private const uint ExitSeed = 0x0badcafe;
    private const int HandleSeed = 0x5a5a5a5a;

    private static void Main(string[] args)
    {
        if (IntPtr.Size != 4) throw new InvalidOperationException("Windows x86 CLR required.");
        nint process = Guest.Process(), thread = Guest.Thread();
        uint threadId = Guest.ThreadId();
        var pseudo = ExitStatus(thread);
        var duplicate = Duplicate(process, thread, process, out nint first);
        var owned = ExitStatus(first);
        var duplicateAgain = Duplicate(process, first, process, out nint second);
        var firstClose = Close(first);
        var stale = ExitStatus(first);
        var otherHandle = ExitStatus(second);
        var invalid = ExitStatus(0);
        var processAsThread = ExitStatus(process);
        var duplicateInvalid = Duplicate(process, 0, process, out _);
        var invalidDestination = Duplicate(process, thread, 0, out _);
        var secondClose = Close(second);
        var closeTwice = Close(second);
        var pseudoClose = Close(thread);
        var afterPseudoClose = ExitStatus(thread);
        var ownedProcess = Duplicate(process, process, process, out nint processCopy);
        var processCopyAsThread = ExitStatus(processCopy);
        var closeProcessCopy = Close(processCopy);
        var stackExit = Guest.ExitCodeOnStack(thread);
        var stackInvalid = Guest.ExitCodeOnStack(0);
        File.WriteAllText(args[0], JsonSerializer.Serialize(new {
            systemSeed = SystemSeed, savedSeed = SavedSeed, exitSeed = ExitSeed, handleSeed = HandleSeed,
            runtime = Environment.Version.ToString(), fixture = typeof(Guest).Assembly.Location,
            cases = new {
                process = (int)process, thread = (int)thread, currentIdStable = threadId > 0 && threadId == Guest.ThreadId(),
                pseudo, duplicate, owned, duplicateAgain, firstClose, stale, otherHandle, invalid, processAsThread,
                duplicateInvalid, invalidDestination, secondClose, closeTwice, pseudoClose, afterPseudoClose,
                ownedProcess, processCopyAsThread, closeProcessCopy, stackExit, stackInvalid
            }
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static object ExitStatus(nint handle)
    {
        uint code = ExitSeed;
        Guest.SeedErrors(SystemSeed, SavedSeed);
        int ok = Guest.ExitCode(handle, (nint)(&code));
        return new { ok, code, system = Guest.SystemError(), saved = Guest.SavedError() };
    }

    private static object Duplicate(nint process, nint thread, nint destination, out nint handle)
    {
        nint value = HandleSeed;
        Guest.SeedErrors(SystemSeed, SavedSeed);
        int ok = Guest.Duplicate(process, thread, destination, (nint)(&value));
        int system = Guest.SystemError(), saved = Guest.SavedError();
        handle = value;
        return new { ok, nonzero = value != 0, unchanged = value == HandleSeed, system, saved };
    }

    private static object Close(nint handle)
    {
        Guest.SeedErrors(SystemSeed, SavedSeed);
        int ok = Guest.Close(handle);
        return new { ok, system = Guest.SystemError(), saved = Guest.SavedError() };
    }
}
