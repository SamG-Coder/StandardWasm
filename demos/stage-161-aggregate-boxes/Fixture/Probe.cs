using System;
using System.Runtime.CompilerServices;

namespace AggregateBoxes
{
    public struct Bounds
    {
        public int X;
        public int Y;
    }

    public struct OtherBounds
    {
        public int X;
        public int Y;
    }

    public struct ReferenceBounds
    {
        public object Tag;
        public int Width;
    }

    public interface IProbe { }

    public struct InterfaceBounds : IProbe
    {
        public int X;
        public int Y;
    }

    public sealed class Cache
    {
        public object Item;
    }

    public static class Probe
    {
        public static int CachedCopy()
        {
            var cache = new Cache();
            var value = new Bounds { X = 19, Y = 23 };
            Put(cache, value);
            value.X = 29;
            Bounds read = Get(cache);
            read.Y = 31;
            return Get(cache).X * 10000 + Get(cache).Y * 100 + read.Y + value.X;
        }

        public static int DistinctBoxes()
        {
            var value = new Bounds { X = 7, Y = 11 };
            object first = value;
            object second = value;
            int different = first != second ? 100 : 0;
            return different + ((Bounds)first).X + ((Bounds)second).Y;
        }

        public static int DefaultBox()
        {
            object boxed = default(Bounds);
            if (boxed == null) return 0;
            Bounds value = (Bounds)boxed;
            return 13 + value.X + value.Y;
        }

        public static int RejectSameShape()
        {
            var cache = new Cache { Item = new OtherBounds { X = 5, Y = 17 } };
            try { return Get(cache).X; }
            catch (InvalidCastException) { return 37; }
        }

        public static int RejectNull()
        {
            var cache = new Cache();
            try { return Get(cache).X; }
            catch (NullReferenceException) { return 41; }
        }

        public static int RejectPrimitiveBox()
        {
            var cache = new Cache { Item = 23 };
            try { return Get(cache).X; }
            catch (InvalidCastException) { return 43; }
        }

        public static int NominalBoxType()
        {
            object value = new Bounds { X = 1, Y = 7 };
            return value is Bounds && !(value is OtherBounds) && value.GetType() == typeof(Bounds) ? 47 : 0;
        }

        public static int ReferenceFieldCopy()
        {
            object tag = new object();
            var value = new ReferenceBounds { Tag = tag, Width = 53 };
            object boxed = value;
            value.Tag = null;
            return ((ReferenceBounds)boxed).Tag == tag ? ((ReferenceBounds)boxed).Width : 0;
        }

        public static int BoxedInterfaceIdentity()
        {
            object matching = new InterfaceBounds { X = 3, Y = 5 };
            object nonmatching = new OtherBounds { X = 3, Y = 5 };
            if (!(matching is IProbe tagged) || nonmatching is IProbe) return 0;
            return tagged.GetType() == typeof(InterfaceBounds) ? 59 : 0;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Put(Cache cache, Bounds value) => cache.Item = value;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Bounds Get(Cache cache) => (Bounds)cache.Item;
    }
}
