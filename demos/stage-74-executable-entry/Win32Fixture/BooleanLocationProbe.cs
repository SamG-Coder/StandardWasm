using System;
using System.Threading;

public static class BooleanLocationProbe
{
    public static int BooleanAliases(int scenario)
    {
        bool first = scenario != 0;
        bool second = false;
        ref bool selected = ref (scenario == 1 ? ref second : ref first);
        bool before = ReadBoolean(ref selected);
        Forward(out selected, scenario == 2);
        int initial = (before ? 4 : 0) + (first ? 2 : 0) + (second ? 1 : 0);
        first = true;
        return initial * 2 + (ReadBoolean(ref selected) ? 1 : 0);
    }

    public static int BooleanWithMonitor(int scenario)
    {
        object target = new object();
        object value = target;
        var view = new ReadOnlySpan<object>(in value);
        bool taken = false;
        Forward(out taken, scenario == 1);
        try
        {
            Monitor.Enter(target, ref taken);
            value = null;
            return ReadBoolean(ref taken) && view[0] == null ? 1 : 0;
        }
        catch (ArgumentException)
        {
            return taken && view[0] == target ? 2 : 0;
        }
        finally
        {
            if (taken && scenario != 1) Monitor.Exit(target);
        }
    }

    private static bool ReadBoolean(ref bool value) => value;
    private static void Forward(out bool value, bool next) => Write(out value, next);
    private static void Write(out bool value, bool next) => value = next;
}
