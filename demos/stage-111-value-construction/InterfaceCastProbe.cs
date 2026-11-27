using System;

namespace ValueConstructionDemo
{
    public static class InterfaceCastProbe
    {
        private interface IReader { int Read(); }
        private interface IDerivedReader : IReader { }

        private class BaseReader : IDerivedReader
        {
            public int Read() => 16;
        }

        private sealed class DerivedReader : BaseReader { }
        private sealed class Unrelated { }

        private sealed class Formatter : ICustomFormatter
        {
            public string Format(string format, object value, IFormatProvider provider) => "format";
        }

        private sealed class DirectReader : IReader
        {
            public int Read() => 32;
        }

        private static int Check(object value)
        {
            IReader tested = value as IReader;
            int result = tested == null ? 0 : 1 | tested.Read();
            if ((object)tested == value) result |= 2;
            try
            {
                IReader cast = (IReader)value;
                if ((object)cast == value) result |= 4;
            }
            catch (InvalidCastException) { result |= 8; }
            return result;
        }

        public static int CheckInterfaceCasts(int mode)
        {
            if (mode < 0) return Check(new DerivedReader());
            if (mode == 0) return Check(null);
            if (mode < 7) return Check(new Unrelated());
            return Check(new DirectReader());
        }

        public static int CheckCoreLibInterfaceCast(int mode)
        {
            if (mode < 0) return CheckFormatter(new Formatter());
            if (mode == 0) return CheckFormatter(null);
            return CheckFormatter(new Unrelated());
        }

        private static int CheckFormatter(object value)
        {
            ICustomFormatter matched = value as ICustomFormatter;
            int result = matched == null ? 0 : matched.Format(null, null, null).Length;
            try
            {
                ICustomFormatter cast = (ICustomFormatter)value;
                return ReferenceEquals(cast, value) ? result + 10 : -3;
            }
            catch (InvalidCastException) { return result + 20; }
        }
    }
}
