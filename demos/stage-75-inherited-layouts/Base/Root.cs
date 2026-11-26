namespace HierarchyBase
{
    public class Root
    {
        public int Value;
        public Root Peer;

        public Root(int value)
        {
            Value = value;
            Peer = this;
        }

        public int Read() => Value;
    }
}
