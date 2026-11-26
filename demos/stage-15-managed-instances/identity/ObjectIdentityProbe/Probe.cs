extern alias ThirdParty;

namespace ObjectIdentityProbe
{
    public sealed class Cell : ThirdParty::System.Object
    {
        private int value;
        public Cell(int seed) => value = seed;
        public int Read() => value;
    }

    public static class Probe
    {
        public static int Make(int seed) => new Cell(seed).Read();
    }
}
