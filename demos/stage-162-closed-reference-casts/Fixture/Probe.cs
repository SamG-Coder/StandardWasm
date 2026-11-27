using System;

namespace ClosedCasts
{
    public interface IBox<T>
    {
        int Read();
    }

    public sealed class Box<T> : IBox<T>
    {
        public int Read() => 103;
    }

    public sealed class GenericCast<T>
    {
        public int Read(object value) => ((IBox<T>)value).Read();
    }

    public static class Probe
    {
        public static int CastClassMatches()
        {
            object value = new Box<int>();
            return ((Box<int>)value).Read() - 2;
        }

        public static int CastInterfaceMatches()
        {
            object value = new Box<int>();
            return ((IBox<int>)value).Read();
        }

        public static int TestClosedIdentity()
        {
            object value = new Box<int>();
            return value is IBox<int> && value is Box<int> && !(value is IBox<long>) &&
                !(value is Box<long>) ? ((Box<int>)value).Read() + 4 : 0;
        }

        public static int CastNull()
        {
            object value = null;
            return (IBox<int>)value == null && (Box<int>)value == null ? 109 : 0;
        }

        public static int WrongClosedCast()
        {
            object value = new Box<int>();
            int caught = 0;
            try { if ((IBox<long>)value != null) return 0; }
            catch (InvalidCastException) { caught++; }
            try { if ((Box<long>)value != null) return 0; }
            catch (InvalidCastException) { caught++; }
            return caught == 2 ? 113 : 0;
        }

        public static int ClosedParameterFromReceiver() => new GenericCast<int>().Read(new Box<int>()) + 24;

        public static int ClosedParameterFromMethod() => ReadAs<long>(new Box<long>()) + 28;

        private static int ReadAs<T>(object value) => ((IBox<T>)value).Read();
    }
}
