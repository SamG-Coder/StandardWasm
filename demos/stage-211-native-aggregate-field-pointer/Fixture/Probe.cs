using System.Runtime.CompilerServices;

namespace NativeAggregateFieldPointer;

public readonly unsafe struct NativePair<T>
{
    public readonly nint Prefix;
    public readonly nint Item;

    public NativePair(nint prefix, nint item)
    {
        Prefix = prefix;
        Item = item;
    }

    public static void** Borrow(in NativePair<T> owner) =>
        (void**)Unsafe.AsPointer(ref Unsafe.AsRef(in owner.Item));
}

public readonly unsafe struct MixedPair
{
    public readonly object Reference;
    public readonly nint Item;

    public static void** Borrow(in MixedPair owner) =>
        (void**)Unsafe.AsPointer(ref Unsafe.AsRef(in owner.Item));
}

public static unsafe class Probe
{
    public static int PointerWrites()
    {
        NativePair<int> owner = default;
        void** pointer = NativePair<int>.Borrow(in owner);
        *pointer = (void*)41;
        return owner.Item == 41 && owner.Prefix == 0 ? 827 : 0;
    }

    public static int ClearAndReborrow()
    {
        NativePair<int> owner = default;
        void** initial = NativePair<int>.Borrow(in owner);
        *initial = (void*)42;
        void** again = NativePair<int>.Borrow(in owner);
        var copy = owner;
        Clear(again);
        return initial == again && *initial == null && owner.Item == 0 && copy.Item == 42 ? 829 : 0;
    }

    public static int IndependentOwners()
    {
        NativePair<int> first = default, second = default;
        void** firstAddress = NativePair<int>.Borrow(in first);
        void** secondAddress = NativePair<int>.Borrow(in second);
        *firstAddress = (void*)51;
        *secondAddress = (void*)52;
        return firstAddress != secondAddress && first.Item == 51 && second.Item == 52 ? 839 : 0;
    }

    public static int TypedReplacement()
    {
        NativePair<int> owner = default;
        void** pointer = NativePair<int>.Borrow(in owner);
        owner = NewPair();
        return owner.Prefix == 62 && *pointer == (void*)63 ? 851 : 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static NativePair<int> NewPair() => new(62, 63);

    public static int ManagedFieldDiagnostic()
    {
        MixedPair owner = default;
        void** pointer = MixedPair.Borrow(in owner);
        *pointer = (void*)73;
        return owner.Reference == null && owner.Item == 73 ? 853 : 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Clear(void** pointer) => *pointer = null;
}
