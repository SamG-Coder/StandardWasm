namespace FieldData
{
    public sealed class Cell
    {
        public long Count;
        public Cell Next;
        public readonly int Seed;

        public Cell(int seed)
        {
            Seed = seed;
            Count = seed;
        }
    }
}
