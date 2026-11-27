namespace ProtectedVirtual
{
    public struct Item
    {
        public int Value;

        public Item(int value) => Value = value;
    }

    public class Collection<T>
    {
        public int Add(T item) => InsertItem(4, item);

        public int AddTo(Collection<T> receiver, T item) => receiver.InsertItem(6, item);

        protected virtual int InsertItem(int index, T item) => index + 301;
    }

    public sealed class SpecialCollection : Collection<Item>
    {
        protected override int InsertItem(int index, Item item) => 401 + index * 10 + item.Value;
    }

    public class DerivedThroughBase : Collection<Item>
    {
        public int AddToDerived(DerivedThroughBase receiver, Item item) => receiver.InsertItem(7, item);
    }

    public static class Probe
    {
        public static int BaseBody() => new Collection<Item>().Add(new Item(5));

        public static int OverrideBody() => new SpecialCollection().Add(new Item(11));

        public static int DynamicReceiver()
        {
            var caller = new Collection<Item>();
            return caller.AddTo(caller, new Item(17)) * 1000 + caller.AddTo(new SpecialCollection(), new Item(19));
        }

        public static int NullReceiver()
        {
            try
            {
                return new Collection<Item>().AddTo(null, new Item(23));
            }
            catch (System.NullReferenceException)
            {
                return 509;
            }
        }

        public static int DerivedAccessDiagnostic()
        {
            var receiver = new DerivedThroughBase();
            return receiver.AddToDerived(receiver, new Item(29));
        }
    }
}
