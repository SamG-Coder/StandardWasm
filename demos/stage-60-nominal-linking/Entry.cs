extern alias bridge;
extern alias leaf;

using LeafCell = leaf::NominalTypes.Cell;
using BridgeCell = bridge::NominalTypes.Cell;
using Router = bridge::NominalTypes.Router;

namespace LinkedNominal
{
    public static class Kernels
    {
        public static int Run(int value, int delta)
        {
            LeafCell first = Router.MakeForeign(value);
            LeafCell second = Router.Forward(first, delta);
            BridgeCell local = Router.MakeLocal(value);
            return unchecked(Router.Read(second) + Router.Read(local));
        }

        public static int Identity(int value)
        {
            LeafCell first = Router.MakeForeign(value);
            LeafCell other = Router.MakeForeign(value);
            LeafCell second = Router.Forward(first, 1);
            return (second == first ? 1 : 0) + (second == other ? 2 : 0) + Router.Read(first);
        }

        public static int NullRead(LeafCell cell) => Router.Read(cell);
    }

    public static class LinkDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] data, int index, int value) => data[index] = unchecked((byte)value);
        public static int ReadByte(byte[] data, int index) => data[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int CompileBundle(byte[] bundle, byte[] entries, byte[] output) =>
            PortableCompiler.MethodCompiler.CompileBundle(bundle, entries, output);
    }
}
