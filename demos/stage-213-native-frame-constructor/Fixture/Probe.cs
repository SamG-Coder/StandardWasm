using System.Runtime.CompilerServices;

namespace NativeFrameConstructor;

public readonly unsafe ref struct Scope<T>
{
    public readonly nint Item;

    public Scope(void* value) => Item = (nint)value;

    public static void** Borrow(in Scope<T> owner) =>
        (void**)Unsafe.AsPointer(ref Unsafe.AsRef(in owner.Item));
}

public readonly unsafe ref struct WordScope
{
    public readonly nint Item;

    public WordScope(nint value) => Item = value;

    public static void** Borrow(in WordScope owner) =>
        (void**)Unsafe.AsPointer(ref Unsafe.AsRef(in owner.Item));
}

public readonly unsafe ref struct ObservableScope
{
    public readonly nint Item;

    public ObservableScope(void* value)
    {
        Item = (nint)value;
        Probe.Observe();
    }

    public static void** Borrow(in ObservableScope owner) =>
        (void**)Unsafe.AsPointer(ref Unsafe.AsRef(in owner.Item));
}

public static unsafe class Probe
{
    private static int observed;

    public static int PointerArgument()
    {
        Scope<string> owner = new((void*)45);
        void** address = Scope<string>.Borrow(in owner);
        if (*address != (void*)45) return 0;
        *address = (void*)73;
        return owner.Item == 73 ? 857 : 0;
    }

    public static int ZeroArgument()
    {
        Scope<int> owner = new((void*)0);
        void** address = Scope<int>.Borrow(in owner);
        return *address == null && owner.Item == 0 ? 859 : 0;
    }

    public static int LoadedArgument() => LoadArgument(91);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int LoadArgument(nint value)
    {
        WordScope owner = new(value);
        void** address = WordScope.Borrow(in owner);
        if (*address != (void*)value) return 0;
        *address = (void*)112;
        return owner.Item == 112 ? 863 : 0;
    }

    public static int IndependentOwners()
    {
        Scope<int> first = new((void*)31);
        Scope<int> second = new((void*)67);
        void** firstAddress = Scope<int>.Borrow(in first);
        void** secondAddress = Scope<int>.Borrow(in second);
        *firstAddress = (void*)42;
        return firstAddress != secondAddress && first.Item == 42 && second.Item == 67 ? 871 : 0;
    }

    public static int ReinitializeBorrowedOwner()
    {
        Scope<int> owner = new((void*)53);
        void** address = Scope<int>.Borrow(in owner);
        owner = new((void*)79);
        void** again = Scope<int>.Borrow(in owner);
        return again == address && *address == (void*)79 && owner.Item == 79 ? 873 : 0;
    }

    public static int SideEffectDiagnostic()
    {
        observed = 0;
        ObservableScope owner = new((void*)32);
        return *ObservableScope.Borrow(in owner) == (void*)32 && observed == 1 ? 877 : 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Observe() => observed++;
}
