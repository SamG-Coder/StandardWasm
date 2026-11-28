using System;

namespace CoreVersionOutFixture
{
    public static class Probe
    {
        public static int Build() => Environment.OSVersion.Version.Build;
    }
}
