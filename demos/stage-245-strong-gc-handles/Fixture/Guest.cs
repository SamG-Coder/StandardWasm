using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public static class Guest
{
    private sealed class Item
    {
        public readonly int Value;
        internal Item(int value) => Value = value;
    }

    public static int Lifecycle()
    {
        var first = new Item(17);
        var second = new Item(29);
        var handle = GCHandle.Alloc(first);
        var alias = GCHandle.FromIntPtr(GCHandle.ToIntPtr(handle));
        bool identity = ReferenceEquals(alias.Target, first);
        alias.Target = second;
        bool replaced = ReferenceEquals(handle.Target, second);
        handle.Free();
        return identity && replaced && !handle.IsAllocated ? second.Value : 0;
    }

    public static int NullTarget()
    {
        var handle = GCHandle.Alloc(null);
        bool allocatedNull = handle.IsAllocated && handle.Target == null;
        handle.Target = new Item(41);
        int value = ((Item)handle.Target).Value;
        handle.Target = null;
        bool updatedNull = handle.IsAllocated && handle.Target == null;
        handle.Free();
        return allocatedNull && updatedNull && !handle.IsAllocated ? value : 0;
    }

    public static int Distinct()
    {
        var item = new Item(73);
        var first = GCHandle.Alloc(item);
        var second = GCHandle.Alloc(item, GCHandleType.Normal);
        bool different = GCHandle.ToIntPtr(first) != GCHandle.ToIntPtr(second);
        first.Free();
        bool stillAlive = ReferenceEquals(second.Target, item);
        second.Free();
        return different && stillAlive ? item.Value : 0;
    }

    public static int FreeTwice()
    {
        var handle = GCHandle.Alloc(null);
        handle.Free();
        try { handle.Free(); }
        catch (InvalidOperationException) { return 83; }
        return 0;
    }

    public static int DefaultTarget()
    {
        GCHandle handle = default;
        try { return handle.Target == null ? 1 : 2; }
        catch (InvalidOperationException) { return 97; }
    }

    public static int Unsupported(int profile)
    {
        try
        {
            var handle = GCHandle.Alloc(null, (GCHandleType)profile);
            handle.Free();
            return 0;
        }
        catch (NotSupportedException) { return 101; }
    }

    public static int InvalidProfile()
    {
        try { GCHandle.Alloc(null, (GCHandleType)17).Free(); }
        catch (ArgumentOutOfRangeException) { return 103; }
        return 0;
    }

    public static int InvalidToken(nint token)
    {
        try { GCHandle.FromIntPtr(token); }
        catch (InvalidOperationException) { return 107; }
        return 0;
    }

    public static int ProbeTarget(nint token)
    {
        try { return ReadTarget(token); }
        catch (InvalidOperationException) { return 109; }
    }

    public static nint PinnedAddress(nint token) => GCHandle.FromIntPtr(token).AddrOfPinnedObject();

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static nint HoldFinalizable(int value) => GCHandle.ToIntPtr(GCHandle.Alloc(new Finalizable(value)));

    public static void Release(nint token)
    {
        var handle = GCHandle.FromIntPtr(token);
        handle.Free();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int ReadTarget(nint token)
    {
        var handle = GCHandle.FromIntPtr(token);
        return ((Finalizable)handle.Target).Value;
    }

    public static int Finalized() => Finalizable.Total;

    private sealed class Finalizable
    {
        internal static int Total;
        internal readonly int Value;
        internal Finalizable(int value) => Value = value;
        ~Finalizable() => Total += Value;
    }
}
