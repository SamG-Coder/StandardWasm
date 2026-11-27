using System;
using System.Runtime.CompilerServices;

namespace ScopedNativeReceiver;

public readonly unsafe ref struct Scope<T>
{
    public readonly nint Item;

    public Scope(void* value) => Item = (nint)value;

    public static void** Borrow(in Scope<T> owner) =>
        (void**)Unsafe.AsPointer(ref Unsafe.AsRef(in owner.Item));

    public void* Value => (void*)Item;

    public void Dispose()
    {
        void* value = (void*)Item;
        *Borrow(in this) = null;
        if (value != null) Probe.Release((nint)value);
    }

    public void** OpenBorrow() => Borrow(in this);
}

public readonly unsafe ref struct InitializedBorrowScope
{
    public readonly nint Item;

    static InitializedBorrowScope() => Probe.ObserveInitialization();

    public static void** Borrow(in InitializedBorrowScope owner) =>
        (void**)Unsafe.AsPointer(ref Unsafe.AsRef(in owner.Item));
}

public static unsafe class Probe
{
    private static int released;
    private static int initialized;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Scope<T> Create<T>(int value) => new((void*)value);

    public static int ValueAndFinally()
    {
        released = 0;
        Scope<string> owner = Create<string>(41);
        int before;
        try
        {
            before = (int)(nint)owner.Value;
        }
        finally
        {
            owner.Dispose();
        }
        return before == 41 && owner.Value == null && released == 41 ? 883 : 0;
    }

    public static int ExceptionalFinally()
    {
        released = 0;
        Scope<int> owner = Create<int>(43);
        try
        {
            try
            {
                _ = CheckedIncrement(int.MaxValue);
            }
            finally
            {
                owner.Dispose();
            }
        }
        catch (OverflowException)
        {
            return owner.Value == null && released == 43 ? 887 : 0;
        }
        return 0;
    }

    public static int IndependentAliases()
    {
        released = 0;
        Scope<string> first = Create<string>(47), second = Create<string>(51);
        void** firstAddress = Scope<string>.Borrow(in first);
        first.Dispose();
        if (*firstAddress != null || second.Value != (void*)51 || released != 47) return 0;
        second.Dispose();
        first.Dispose();
        return second.Value == null && released == 98 ? 907 : 0;
    }

    public static int EscapedAddressDiagnostic()
    {
        Scope<string> owner = Create<string>(61);
        void** escaped = owner.OpenBorrow();
        int value = (int)(nint)(*escaped);
        owner.Dispose();
        return value == 61 ? 911 : 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Release(nint value) => released += (int)value;

    public static int InitializedBorrowDiagnostic()
    {
        initialized = 0;
        InitializedBorrowScope scope = default;
        void** pointer = InitializedBorrowScope.Borrow(in scope);
        return *pointer == null && initialized == 1 ? 919 : 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ObserveInitialization() => initialized++;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int CheckedIncrement(int value) => checked(value + 1);
}
