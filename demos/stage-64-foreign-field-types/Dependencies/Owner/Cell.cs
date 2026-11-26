using ForeignFieldLeaf;

namespace ForeignFieldOwner
{
    public sealed class Cell
    {
        public Node Child;
        public Node Alternate;

        public int Read() => Child.Value;
    }
}
