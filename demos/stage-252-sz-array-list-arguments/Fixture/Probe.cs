using System.Collections.Generic;

namespace ArrayListArguments
{
    public struct Value
    {
        public int Number;
    }

    public sealed class Reference
    {
        public int Number;
    }

    public static class Probe
    {
        private static int ObserveList(IList<Value> view, object source) =>
            view == null ? 17 : ReferenceEquals(view, source) ? 101 : -1;

        private static int ObserveCollection(ICollection<Reference> view, object source) =>
            view == null ? 19 : ReferenceEquals(view, source) ? 103 : -1;

        public static int ValueList()
        {
            var values = new Value[2];
            return ObserveList(values, values);
        }

        public static int ReferenceCollection()
        {
            var values = new Reference[1];
            return ObserveCollection(values, values);
        }

        public static int NullList() => ObserveList(null, null);

        public static int CountRequiresDispatch()
        {
            ICollection<Value> values = new Value[3];
            return values.Count;
        }
    }
}
