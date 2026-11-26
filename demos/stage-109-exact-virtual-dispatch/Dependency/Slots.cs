namespace VirtualDispatchDependency
{
    public class Root
    {
        public virtual int Read(int value) => unchecked(value + 1);
    }

    public class Intermediate : Root
    {
        public override int Read(int value) => unchecked(value + 11);
    }

    public sealed class Inherited : Intermediate { }

    public class Hiding : Intermediate
    {
        public new virtual int Read(int value) => unchecked(value + 31);
    }

    public sealed class HiddenLeaf : Hiding
    {
        public override int Read(int value) => unchecked(value + 41);
    }
}
