namespace DispatchDependency
{
    public class Item
    {
        public virtual int Read(int input) => unchecked(input + 10);
    }

    public class DerivedItem : Item
    {
        public override int Read(int input) => unchecked(input + 20);
    }

    public class HiddenItem : DerivedItem
    {
        public new virtual int Read(int input) => unchecked(input + 30);
    }

    public sealed class HiddenLeaf : HiddenItem
    {
        public override int Read(int input) => unchecked(input + 40);
    }

    public sealed class Provider
    {
        public Provider(Item item) { Current = item; }
        public Item Current { get; }
    }

    public abstract class Maker
    {
        public abstract Item Build();
    }

    public class LateMaker : Maker
    {
        public override Item Build() => new LateItem();
    }

    public sealed class LateItem : Item
    {
        public override int Read(int input) => unchecked(input + 80);
    }
}
