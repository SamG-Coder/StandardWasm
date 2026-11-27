using System;

namespace ValueConstructionDemo
{
    public static class ObjectTypeProbe
    {
        private interface IMarker { }
        private class Marker : IMarker { }
        private sealed class Derived : Marker { }

        private static int Identify(object value)
        {
            try
            {
                Type actual = value.GetType();
                if (actual == typeof(Derived)) return 1;
                if (actual == typeof(object)) return 2;
                if (actual == typeof(string)) return 3;
                if (actual == typeof(int)) return 4;
                if (actual == typeof(uint)) return 5;
                return -1;
            }
            catch (NullReferenceException) { return -7; }
        }

        private static int IdentifyInterface(IMarker marker) => marker.GetType() == typeof(Derived) ? 11 : 0;
        private static bool Matches<T>(string value) => value.GetType() == typeof(T);

        public static int CheckObjectType(int mode)
        {
            if (mode == int.MinValue) return Identify(null);
            if (mode < 0)
            {
                var marker = new Derived();
                return Identify(marker) + IdentifyInterface(marker);
            }
            if (mode == 0) return Identify(new object());
            if (mode < 7) return Identify(mode) * 10 + Identify((uint)mode);
            return Identify("text") + (Matches<string>("text") ? 100 : 0);
        }
    }
}
