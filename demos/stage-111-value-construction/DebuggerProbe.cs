using System.Diagnostics;

public static class DebuggerProbe
{
    public static int ReadManagedDebuggerStatus() => Debugger.IsAttached ? 1 : 0;
}
