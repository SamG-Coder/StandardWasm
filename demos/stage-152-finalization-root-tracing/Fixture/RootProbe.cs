using System;

namespace ManagedFinalizationExperiment
{
    public static class RootProbe
    {
        private static object saved;

        public static object SetStaticRoot(object value)
        {
            object previous = saved;
            saved = value;
            return previous;
        }

        public static int ObserveFrames(int seed)
        {
            var receiver = new Receiver { Value = seed };
            // receiver survives below both call operands and is read once more after the call.
            return Add(receiver, ObserveNested(receiver)) + receiver.Value;
        }

        private static int ObserveNested(Receiver consumed)
        {
            var local = new Receiver { Value = 7 };
            Notify();
            return local.Value;
        }

        private static int Add(Receiver value, int other) => value.Value + other;
        private static void Notify() => Console.Out.WriteLine();

        public static int ObserveConstructor(int seed) => new ConstructorReceiver(seed).Value;

        public static object BuildGraph(int seed)
        {
            var root = new Graph { Scalar = seed, Inherited = new Receiver { Value = seed } };
            var peer = new Graph { Next = root };
            root.Next = peer;
            root.Items = new Graph[] { peer, null, root };
            root.Payload = new byte[2];
            Notify();
            return root;
        }

        private class GraphBase
        {
            internal int Scalar;
            internal Receiver Inherited;
        }

        private sealed class Graph : GraphBase
        {
            internal Graph Next;
            internal Graph[] Items;
            internal Graph Missing = null;
            internal byte[] Payload;
        }

        private sealed class ConstructorReceiver
        {
            internal readonly int Value;

            internal ConstructorReceiver(int value)
            {
                Notify();
                Value = value;
            }
        }

        public static int ThrowWithRoot(int seed)
        {
            var receiver = new Receiver { Value = seed };
            Throw();
            return receiver.Value;
        }

        private static void Throw()
        {
            Notify();
            throw null;
        }
    }
}
