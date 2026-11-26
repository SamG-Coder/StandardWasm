namespace PackedFieldDependency
{
    public class Register
    {
        public sbyte SignedByte;
        public byte UnsignedByte;
        public short SignedShort;
        public ushort UnsignedShort;
        public char Character;
        public bool Flag;
        public uint Word;
        public int Marker;

        public Register(int marker) => Marker = marker;
    }
}
