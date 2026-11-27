using System;
using System.Runtime.CompilerServices;

namespace VolatileReferenceFields;

public sealed class Node<T>
{
    public Node(T item) => Item = item;
    public readonly T Item;
}

public struct Bucket<T>
{
    public volatile Node<T> Node;
    public int Stamp;
}

public struct Counter
{
    public volatile int Number;
}

public static class Probe
{
    public static int ReplacedBucketAliases()
    {
        var buckets = new Bucket<int>[2];
        var original = new Node<int>(11);
        var neighbor = new Node<int>(31);
        var replacement = new Node<int>(47);
        var last = new Node<int>(61);
        buckets[0] = new Bucket<int> { Node = original, Stamp = 13 };
        buckets[1] = new Bucket<int> { Node = neighbor, Stamp = 37 };
        ref Node<int> first = ref FieldAt(buckets, 0);
        ref Node<int> same = ref FieldAt(buckets, 0);
        ref Node<int> next = ref FieldAt(buckets, 1);
        var snapshot = buckets[0];
        buckets[0] = new Bucket<int> { Node = replacement, Stamp = 53 };
        bool replacementObserved = ReferenceEquals(first, replacement);
        same = last;
        return replacementObserved && ReferenceEquals(first, last) && ReferenceEquals(buckets[0].Node, last) &&
            ReferenceEquals(next, neighbor) && ReferenceEquals(snapshot.Node, original) &&
            buckets[0].Stamp == 53 && buckets[1].Stamp == 37 && snapshot.Stamp == 13 ? last.Item : 0;
    }

    public static int DefaultsAndNullWrites()
    {
        var buckets = new Bucket<long>[1];
        ref Node<long> first = ref FieldAt(buckets, 0);
        bool initiallyNull = first == null;
        var node = new Node<long>(73);
        buckets[0].Node = node;
        bool directWriteObserved = ReferenceEquals(first, node);
        first = null;
        return initiallyNull && directWriteObserved && buckets[0].Node == null ? (int)node.Item : 0;
    }

    public static int VolatileValueAddress()
    {
        var counters = new Counter[1];
        ref int number = ref NumberAt(counters, 0);
        number = 91;
        return counters[0].Number;
    }

    // Matches ConcurrentDictionary's ref-return shape. The caller chooses
    // the ordering of operations through the returned address.
#pragma warning disable CS0420
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ref Node<T> FieldAt<T>(Bucket<T>[] buckets, int index) => ref buckets[index].Node;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ref int NumberAt(Counter[] counters, int index) => ref counters[index].Number;
#pragma warning restore CS0420
}
