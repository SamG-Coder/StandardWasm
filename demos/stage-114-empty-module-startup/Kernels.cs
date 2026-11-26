using System;
using System.Runtime.CompilerServices;

namespace EmptyModuleStartup
{
    internal static class Setup
    {
        [ModuleInitializer]
        internal static void Initialize() { }
    }

    public static class Kernels
    {
        public static int Twice(int value) => unchecked(2 * value);
    }
}

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class ModuleInitializerAttribute : Attribute { }
}
