using System;

namespace ManagedFinalizationExperiment
{
    public static class RootProbe
    {
        private static object saved;
        private static int addressedValue;

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
        public static int ConstructValue(int seed) => new ValueBox(seed).Value;

        public static string MakePinOwner(int first)
        {
            var characters = new char[1];
            characters[0] = (char)first;
            return new string(characters);
        }

        public static unsafe int ObservePinned(string owner, int throwAfterObservation)
        {
            fixed (char* text = owner)
            {
                Notify();
                if (throwAfterObservation != 0) Raise();
                return text[0];
            }
        }

        private static void Raise() => throw null;

        public static int ObserveProtectedException(int divisor)
        {
            try
            {
                try
                {
                    if (divisor != 0) ThrowManaged();
                    return 1 / divisor;
                }
                finally { Notify(); }
            }
            catch (Exception error) when (error is ProbeException or DivideByZeroException)
            {
                Notify();
                throw;
            }
        }

        private static void ThrowManaged() => throw new ProbeException();

        private sealed class ProbeException : Exception { }

        public static int ObserveInitializerEntry(int seed) =>
            PreciseInitializer.Read(new Receiver { Value = seed });

        public static int ObserveInitializerField(int seed)
        {
            FieldInitializer.Value = new Receiver { Value = seed };
            int value = FieldInitializer.Value.Value;
            FieldInitializer.Value = null;
            return value;
        }

        public static int ObserveInitializerFailure(int seed)
        {
            var caller = new Receiver { Value = seed };
            return FailingInitializer.Read(new Receiver { Value = seed + 1 }) + caller.Value;
        }

        private static class PreciseInitializer
        {
            static PreciseInitializer() { Notify(); }
            internal static int Read(Receiver receiver) => receiver.Value;
        }

        private static class FieldInitializer
        {
            internal static Receiver Value = Initialize();
            private static Receiver Initialize() { Notify(); return null; }
        }

        private static class FailingInitializer
        {
            static FailingInitializer() { Notify(); Raise(); }
            internal static int Read(Receiver receiver) => receiver.Value;
        }

        public static int ObserveAddressedFrame(int seed)
        {
            AddressedScope scope = default;
            try
            {
                scope = new AddressedScope(seed);
                Notify();
            }
            finally { scope.Observe(); }
            return addressedValue;
        }

        public static int ObserveReferenceCell(int seed)
        {
            object slot = null;
            try
            {
                Replace(ref slot, new Receiver { Value = seed });
                Notify();
            }
            finally { ObserveCell(ref slot); }
            return addressedValue;
        }

        private static void Replace(ref object slot, object value) => slot = value;

        private static void ObserveCell(ref object slot)
        {
            Notify();
            addressedValue = ((Receiver)slot).Value;
            slot = null;
        }

        private struct AddressedScope
        {
            internal Receiver Target;
            internal AddressedScope(int value) => Target = new Receiver { Value = value };

            internal void Observe()
            {
                Notify();
                addressedValue = Target.Value;
            }
        }

        private struct ValueBox
        {
            internal readonly int Value;
            internal ValueBox(int value) { Value = value; }
        }

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
