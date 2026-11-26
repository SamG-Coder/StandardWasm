extern alias leaf;

using LeafCell = leaf::NominalTypes.Cell;
using LeafApi = leaf::NominalTypes.Leaf;

namespace NominalTypes
{
    public sealed class Cell
    {
        public long Count;

        public int Score() => unchecked((int)Count + 7);
    }

    public static class Router
    {
        public static LeafCell MakeForeign(int value) => LeafApi.Make(value);

        public static Cell MakeLocal(int value) => new Cell { Count = (long)value * 2 };

        public static LeafCell Forward(LeafCell cell, int delta)
        {
            LeafCell shared = LeafApi.Bump(cell, delta);
            return shared;
        }

        public static int Read(LeafCell cell) => unchecked(LeafApi.Read(cell) + 100);

        public static int Read(Cell cell) => unchecked(cell.Score() + 200);
    }
}
