using System.Runtime.CompilerServices;

namespace ScopedNativeFrameLayout;

public readonly unsafe ref struct Scope<T>
{
    public readonly int Stamp;
    public readonly nint Handle;

    public static void** Borrow(in Scope<T> owner) =>
        (void**)Unsafe.AsPointer(ref Unsafe.AsRef(in owner.Handle));
}

public readonly unsafe ref struct ManagedScope
{
    public readonly object Reference;
    public readonly nint Handle;

    public static void** Borrow(in ManagedScope owner) =>
        (void**)Unsafe.AsPointer(ref Unsafe.AsRef(in owner.Handle));
}

public readonly unsafe ref struct ByrefScope
{
    public readonly ref int Reference;
    public readonly nint Handle;

    public static void** Borrow(in ByrefScope owner) =>
        (void**)Unsafe.AsPointer(ref Unsafe.AsRef(in owner.Handle));
}

public static unsafe class Probe
{
    public static int WriteAndClear()
    {
        Scope<string> owner = default;
        void** borrowed = Scope<string>.Borrow(in owner);
        if (*borrowed != null || owner.Stamp != 0) return 0;
        *borrowed = (void*)71;
        if (owner.Handle != 71 || owner.Stamp != 0) return 0;
        Clear(borrowed);
        return owner.Handle == 0 && owner.Stamp == 0 ? 859 : 0;
    }

    public static int IndependentOwners()
    {
        Scope<string> first = default, second = default;
        void** firstAddress = Scope<string>.Borrow(in first);
        void** secondAddress = Scope<string>.Borrow(in second);
        *firstAddress = (void*)82;
        *secondAddress = (void*)83;
        return firstAddress != secondAddress && first.Handle == 82 && second.Handle == 83 ? 863 : 0;
    }

    public static int ReborrowAfterReplacement()
    {
        Scope<int> owner = default;
        void** address = Scope<int>.Borrow(in owner);
        *address = (void*)91;
        Scope<int> copy = owner;
        owner = default;
        void** again = Scope<int>.Borrow(in owner);
        return again == address && *address == null && copy.Handle == 91 ? 877 : 0;
    }

    public static int ManagedFieldDiagnostic()
    {
        ManagedScope owner = default;
        void** address = ManagedScope.Borrow(in owner);
        *address = (void*)93;
        return owner.Reference == null && owner.Handle == 93 ? 881 : 0;
    }

    public static int ByrefFieldDiagnostic()
    {
        ByrefScope owner = default;
        void** address = ByrefScope.Borrow(in owner);
        *address = (void*)94;
        return owner.Handle == 94 ? 883 : 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Clear(void** address) => *address = null;
}
