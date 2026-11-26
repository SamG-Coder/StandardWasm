extern alias ThirdParty;

namespace TextIdentityProbe
{
    public static class Kernels
    {
        public static int ConcatLength() => ThirdParty::System.String.Concat("a", "b").Length;

        public static int Construct() => new ThirdParty::System.String(new char[3]).Length;

        public static int Allocate()
        {
            _ = new ThirdParty::System.Char[3];
            return 3;
        }
    }
}
