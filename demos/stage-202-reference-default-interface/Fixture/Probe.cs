using System;
using System.Runtime.CompilerServices;

namespace ReferenceDefaultInterface;

public interface ILease<T>
{
    object Owner => this;
}

public interface ILeft : ILease<int> { }
public interface IRight : ILease<int> { }

public sealed class DefaultLease : ILease<int> { }
public sealed class DiamondLease : ILeft, IRight { }

public class MappedLease : ILease<int>
{
    private readonly object owner;

    public MappedLease(object owner) => this.owner = owner;

    public object Owner => owner;
}

public sealed class InheritedLease : MappedLease
{
    public InheritedLease(object owner) : base(owner) { }
}

public sealed class ExplicitLease : ILease<int>
{
    private readonly object owner;

    public ExplicitLease(object owner) => this.owner = owner;

    object ILease<int>.Owner => owner;
}

public interface IReplaced : ILease<int>
{
    object ILease<int>.Owner => null;
}

public sealed class ReplacedLease : IReplaced { }

public interface ICount
{
    int Count => 757;
}

public sealed class DefaultCount : ICount { }

public interface IForwardingLease
{
    object Value { get; }
    object Owner => Value;
}

public sealed class ForwardingLease : IForwardingLease
{
    public ForwardingLease(object value) => Value = value;

    public object Value { get; }
}

public static class Probe
{
    public static int DefaultIdentity()
    {
        var lease = new DefaultLease();
        return Read(lease) == lease ? 719 : 0;
    }

    public static int SharedDefault()
    {
        var lease = new DiamondLease();
        return Read(lease) == lease ? 727 : 0;
    }

    public static int ClassPriority()
    {
        object expected = new object();
        return Read(new InheritedLease(expected)) == expected ? 733 : 0;
    }

    public static int ExplicitPriority()
    {
        object expected = new object();
        return Read(new ExplicitLease(expected)) == expected ? 739 : 0;
    }

    public static int NullReceiver()
    {
        try { return Read<DefaultLease>(null) == null ? 0 : 1; }
        catch (NullReferenceException) { return 743; }
    }

    public static int ScalarDefaultBody() => Count(new DefaultCount());

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Count<T>(T value) where T : ICount => value.Count;

    public static int ForwardedBody()
    {
        object expected = new object();
        IForwardingLease lease = new ForwardingLease(expected);
        return Forward(lease) == expected ? 761 : 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static object Forward(IForwardingLease lease) => lease.Owner;

    public static int MoreSpecificDefault() =>
        ReadReplaced(new ReplacedLease()) == null ? 751 : 0;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static object Read<T>(T lease) where T : ILease<int> => lease.Owner;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static object ReadReplaced(ReplacedLease lease) => Read(lease);
}
