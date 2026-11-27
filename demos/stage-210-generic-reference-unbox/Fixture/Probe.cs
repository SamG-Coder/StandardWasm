using System;
using System.Runtime.CompilerServices;

namespace GenericReferenceUnbox
{
    public static class Probe
    {
        private interface IMarker { int Read(); }

        private class Marker : IMarker
        {
            public int Read() => 821;
        }

        private sealed class Derived : Marker { }
        private sealed class Unrelated { }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static T Cast<T>(object value) => (T)value;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool TryRead<T>(object value, out T result)
        {
            if (value is T)
            {
                result = (T)value;
                return true;
            }
            result = default;
            return false;
        }

        public static int DerivedIdentity()
        {
            object source = new Derived();
            return source == Cast<Marker>(source) ? 1 : 0;
        }

        public static int InterfaceIdentity()
        {
            object source = new Derived();
            return source == Cast<IMarker>(source) ? 1 : 0;
        }

        public static int InterfacePattern()
        {
            object source = new Derived();
            return TryRead<IMarker>(source, out IMarker result) && result == source ? 1 : 0;
        }

        public static int NullValue() => Cast<Marker>(null) == null ? 1 : 0;

        public static int StringIdentity()
        {
            object source = "StandardWasm";
            return source == (object)Cast<string>(source) ? 1 : 0;
        }

        public static int ObjectIdentity()
        {
            object source = new Derived();
            return source == Cast<object>(source) ? 1 : 0;
        }

        public static int InvalidInterface()
        {
            try
            {
                Cast<IMarker>(new Unrelated());
                return 0;
            }
            catch (InvalidCastException)
            {
                return 1;
            }
        }

        public static int ValueTypeStillUnboxes() => Cast<int>(827);
    }
}
