using NativeIntegerVectors;

namespace LinkedNativeVectors
{
    public static class Entry
    {
        public static int FieldAlias(int value)
        {
            var holder = new Carrier(2);
            nint[] alias = holder.Cells;
            alias[1] = value;
            return (int)holder.Cells[1] + (int)holder.Cells[0];
        }
    }
}
