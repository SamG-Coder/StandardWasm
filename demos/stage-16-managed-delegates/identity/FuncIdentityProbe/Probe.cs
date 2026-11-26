extern alias ThirdParty;

namespace FuncIdentityProbe
{
    public static class Probe
    {
        public static int Invoke(ThirdParty::System.Func<int> callback) => callback();
    }
}
