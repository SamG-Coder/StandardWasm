using System;
using System.Runtime.CompilerServices;

namespace ExactTypeHandle
{
    public sealed class Marker { }
    public sealed class Box<T> { }

    public class OtherHandle
    {
        public virtual RuntimeTypeHandle TypeHandle => typeof(Box<int>).TypeHandle;
    }

    public static class Probe
    {
        public static int Nominal() =>
            ReferenceEquals(typeof(Marker), Type.GetTypeFromHandle(typeof(Marker).TypeHandle)) ? 953 : 0;

        public static int ClosedGeneric()
        {
            Type first = Type.GetTypeFromHandle(typeof(Box<int>).TypeHandle);
            Type second = Type.GetTypeFromHandle(typeof(Box<uint>).TypeHandle);
            return ReferenceEquals(first, typeof(Box<int>)) &&
                ReferenceEquals(second, typeof(Box<uint>)) && !ReferenceEquals(first, second) ? 967 : 0;
        }

        public static int GenericParameter() => RoundTrip<Marker>() + RoundTrip<int>();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int RoundTrip<T>() =>
            ReferenceEquals(typeof(T), Type.GetTypeFromHandle(typeof(T).TypeHandle)) ? 491 : 0;

        public static int OtherDeclaringType()
        {
            var value = new OtherHandle();
            return ReferenceEquals(typeof(Box<int>), Type.GetTypeFromHandle(value.TypeHandle)) ? 977 : 0;
        }

        public static int UnprovedReceiverDiagnostic() => ReadHandle(typeof(Marker));

        public static int BranchReceiverDiagnostic() => BranchReceiver(true);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int BranchReceiver(bool choose) =>
            ReferenceEquals(typeof(Marker), Type.GetTypeFromHandle((choose ? typeof(Marker) : typeof(Box<int>)).TypeHandle))
                ? 983 : 0;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ReadHandle(Type receiver) =>
            ReferenceEquals(typeof(Marker), Type.GetTypeFromHandle(receiver.TypeHandle)) ? 991 : 0;
    }
}
