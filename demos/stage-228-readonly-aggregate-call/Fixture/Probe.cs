using System;
using System.Runtime.CompilerServices;

namespace ReadOnlyAggregateCall
{
    public sealed class Payload
    {
        public readonly int Code;
        public Payload(int code) => Code = code;
    }

    public struct State<T>
    {
        public T Payload;
        public int Count;
    }

    public sealed class Owner<T>
    {
        public readonly State<T> State;
        public Owner(T payload, int count) => State = new State<T> { Payload = payload, Count = count };
    }

    public sealed class ProbeException : Exception
    {
        public Payload Payload;
        public int Count;

        internal void Restore(in State<Payload> state)
        {
            Payload = state.Payload;
            Count = state.Count;
        }
    }

    public sealed class Reader
    {
        internal int Observe(in State<Payload> state) => state.Count + state.Payload.Code;
    }

    public static class Probe
    {
        public static int ReadClassAggregate()
        {
            var owner = new Owner<Payload>(new Payload(101), 12);
            return Read(in owner.State);
        }

        public static int PreserveThrownIdentity()
        {
            var payload = new Payload(17);
            var owner = new Owner<Payload>(payload, 71);
            var original = new ProbeException();
            try
            {
                original.Restore(in owner.State);
                throw original;
            }
            catch (ProbeException caught)
            {
                return ReferenceEquals(caught, original) && ReferenceEquals(caught.Payload, payload)
                    ? caught.Payload.Code + caught.Count : 0;
            }
        }

        public static int NullFieldReceiver()
        {
            Owner<Payload> owner = null;
            try { return Read(in owner.State); }
            catch (NullReferenceException) { return 131; }
        }

        public static int NullCallReceiver()
        {
            Reader reader = null;
            var owner = new Owner<Payload>(new Payload(15), 119);
            try { return reader.Observe(in owner.State); }
            catch (NullReferenceException) { return 137; }
        }

        public static int ForwardedAddressDiagnostic()
        {
            var owner = new Owner<Payload>(new Payload(13), 129);
            return Forward(in owner.State);
        }

        public static int MutatingAddressDiagnostic()
        {
            var owner = new Owner<Payload>(new Payload(3), 17);
            return Mutate(in owner.State);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int Read(in State<Payload> state) => state.Count + state.Payload.Code;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int Forward(in State<Payload> state) => Read(in state);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static unsafe int Mutate(in State<Payload> state)
        {
            fixed (int* pointer = &state.Count)
                *pointer += 132;
            return state.Count;
        }
    }
}
