namespace NominalTypes
{
    public sealed class Cell
    {
        public int Value;
    }

    public static class Leaf
    {
        public static Cell Make(int value) => new Cell { Value = value };

        public static Cell Bump(Cell cell, int delta)
        {
            cell.Value = unchecked(cell.Value + delta);
            return cell;
        }

        public static int Read(Cell cell) => cell.Value;
    }
}
