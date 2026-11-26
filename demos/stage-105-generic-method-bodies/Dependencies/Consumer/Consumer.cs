using GenericBodyDefinition;

namespace GenericBodyConsumer
{
    // A second assembly names the same closed instantiations as the app. Both
    // must bind one layout and one specialized body per closed declaring type.
    public static class Consumer
    {
        public static Holder<uint> MakeUnsigned(uint value) => new Holder<uint>(value);

        public static int ReadSigned(Holder<int> holder) => holder.Get();

        public static int Halve(Holder<uint> holder)
        {
            holder.Set(holder.Get() / 2u);
            return holder.Writes;
        }
    }
}
