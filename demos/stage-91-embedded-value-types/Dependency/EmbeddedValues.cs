namespace EmbeddedValueDependency
{
    public struct Values
    {
        public int First;
        public short Second;
    }

    public struct OtherValues
    {
        public int First;
        public short Second;
    }

    public unsafe readonly struct ContextHandle
    {
        public readonly void* Value;
    }

    public sealed class Frame
    {
        public Values State;
        public ContextHandle Context;
        public int Marker;

        public Frame(int marker) => Marker = marker;
    }

    public static class ValueCalls
    {
        public static Values Identity(Values value) => value;
    }
}
