namespace ValueConstructionDependency
{
    public struct StoredValue
    {
        public int Number;
    }

    public sealed class Entries<T>
    {
        private T[] values;

        public Entries(T[] values) => this.values = values;
        public ref T At(int index) => ref values[index];
    }

    public static class ReturnedLocations
    {
        public static ref object At(object[] values, int index) => ref values[index];
    }
}
