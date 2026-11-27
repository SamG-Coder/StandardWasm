using System;

namespace FalseBooleanSpecialization;

public sealed class Probe
{
    private static int State;
    private readonly int value;

    private Probe(int value) => this.value = value;

    private static int Unsupported()
    {
        var matrix = new int[1, 1];
        matrix[0, 0] = 73;
        return matrix[0, 0];
    }

    private static int Guard(bool include) => include ? Unsupported() : 17;

    private static int First() => Guard(false);

    private static int Second() => Guard(false) + 1;

    public static int IndependentCallers() => First() + Second();

    private int InstanceGuard(bool include) => include ? Unsupported() : value;

    private int ObserveInstance() => InstanceGuard(false);

    private static Probe MakeReceiver()
    {
        State += 13;
        return new Probe(31);
    }

    public static int ReceiverEvaluation()
    {
        State = 0;
        return MakeReceiver().ObserveInstance() + State;
    }

    private static int RecursiveGuard(bool include)
    {
        if (include) return Unsupported();
        if (State == 0) return 1;
        State--;
        return RecursiveGuard(false) + 1;
    }

    public static int Recursion()
    {
        State = 6;
        return RecursiveGuard(false);
    }

    private static int Plain(bool include) => include ? 63 : 49;

    private static bool ObserveFlag()
    {
        State += 11;
        return true;
    }

    public static int OrdinaryAndClone()
    {
        State = 0;
        return Plain(false) + Plain(true) + Plain(ObserveFlag()) + State;
    }

    public static unsafe int MethodAddress()
    {
        delegate* managed<bool, int> address = &Plain;
        return Plain(false) + address(false) + address(true);
    }

    private static int LoopGuard(bool include)
    {
    Recheck:
        if (include) return 23;
        if (State != 0) return 97;
        State = 1;
        include = true;
        goto Recheck;
    }

    public static int AlternateEntry()
    {
        State = 0;
        return LoopGuard(false);
    }

    private static int ThrowOrReturn(bool trigger)
    {
        if (trigger) throw null;
        return 17;
    }

    public static int ExceptionEvaluation()
    {
        int result = ThrowOrReturn(false);
        try { return result + ThrowOrReturn(true); }
        catch (NullReferenceException) { return result + 29; }
    }

    public static int TruePathRequiresUnsupported() => Guard(true);

    public static int DynamicPathRequiresUnsupported() => Guard(ObserveFlag());
}
