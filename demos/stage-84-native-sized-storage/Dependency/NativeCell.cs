namespace NativeLayoutDependency
{
    public sealed class NativeCell
    {
        public int Marker;
        public nint Signed;
        public nuint Unsigned;

        public NativeCell(int marker) => Marker = marker;

        public nint ReadSigned() => Signed;
        public nuint ReadUnsigned() => Unsigned;
    }
}
