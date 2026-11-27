using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace BooleanStorageAliases;

internal sealed class Owner
{
    internal bool First;
    internal bool Second;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Wrapper
{
    internal bool Field;
}

public static class Probe
{
    private static bool Read(ref bool value) => Unsafe.As<bool, Wrapper>(ref value).Field;

    private static void Write(ref bool destination, bool value) => Unsafe.As<bool, Wrapper>(ref destination).Field = value;

    public static int OrdinaryWrapper()
    {
        bool local = false;
        var owner = new Owner();
        Write(ref local, true);
        Write(ref owner.First, true);
        bool previous = Read(ref local) && Read(ref owner.First);
        Write(ref owner.First, false);
        Write(ref owner.Second, true);
        return previous && local && !owner.First && owner.Second && !Read(ref owner.First) ? 131 : 0;
    }

    public static int OriginalVolatileClass()
    {
        var owner = new Owner();
        Volatile.Write(ref owner.First, true);
        bool previous = owner.First;
        Volatile.Write(ref owner.First, false);
        return previous && !owner.First ? 137 : 0;
    }

    public static int OriginalVolatileLocal()
    {
        bool local = false;
        Volatile.Write(ref local, true);
        return local ? 139 : 0;
    }

    public static int TypedNull()
    {
        int score = 0;
        try
        {
            Read(ref Unsafe.NullRef<bool>());
        }
        catch (NullReferenceException)
        {
            score += 71;
        }
        try
        {
            Volatile.Write(ref Unsafe.NullRef<bool>(), true);
        }
        catch (NullReferenceException)
        {
            score += 78;
        }
        return score;
    }

    private static void Publish(ref Wrapper wrapper) => wrapper.Field = true;

    public static int EscapingWrapper()
    {
        bool local = false;
        Publish(ref Unsafe.As<bool, Wrapper>(ref local));
        return local ? 151 : 0;
    }
}
