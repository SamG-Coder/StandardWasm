namespace NullStackDependency
{
    public sealed class RemoteMeter
    {
        private int value;

        public RemoteMeter(int value) => this.value = value;
        public int Read() => value;
    }
}
