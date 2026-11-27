using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace InlineReferenceArrayConstructors;

[InlineArray(3)]
[StructLayout(LayoutKind.Auto)]
public struct Slots
{
    private object first;

    public Slots(object initial, object middle, object last)
    {
        this = default;
        this[0] = initial;
        this[1] = middle;
        this[2] = last;
    }

    public Slots(object middle)
    {
        this = default;
        this[0] = null;
        this[1] = middle;
    }
}

[InlineArray(2)]
[StructLayout(LayoutKind.Auto)]
public struct GenericSlots<T>
{
    private T first;

    public GenericSlots(T initial, T last)
    {
        this = default;
        this[0] = initial;
        this[1] = last;
    }
}

[InlineArray(2)]
[StructLayout(LayoutKind.Auto)]
public struct EffectfulSlots
{
    private object first;

    public EffectfulSlots(object value)
    {
        this = default;
        this[0] = Observe(value);
        this[1] = value;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static object Observe(object value) => value;
}

public static class Probe
{
    public static int ConstructorSlots()
    {
        object first = new object(), middle = new object(), last = new object();
        var slots = new Slots(first, middle, last);
        ReadOnlySpan<object> view = slots;
        return view[0] == first && view[1] == middle && view[2] == last ? 311 : 0;
    }

    public static int DefaultAndNull()
    {
        object middle = new object();
        var slots = new Slots(middle);
        ReadOnlySpan<object> view = slots;
        return view[0] == null && view[1] == middle && view[2] == null ? 313 : 0;
    }

    public static int CopyAfterConstruction()
    {
        object shared = new object(), replacement = new object();
        var source = new Slots(shared, shared, null);
        Slots copy = source;
        copy[1] = replacement;
        ReadOnlySpan<object> original = source, changed = copy;
        return original[0] == shared && original[1] == shared &&
            changed[0] == shared && changed[1] == replacement && changed[2] == null ? 317 : 0;
    }

    public static int ClosedGenericSlots()
    {
        object first = new object(), last = new object();
        var slots = new GenericSlots<object>(first, last);
        ReadOnlySpan<object> view = slots;
        return view[0] == first && view[1] == last ? 319 : 0;
    }

    public static int UnprovenStore()
    {
        object value = new object();
        var slots = new EffectfulSlots(value);
        return slots[1] == value ? 331 : 0;
    }
}
