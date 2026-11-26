using System;
using System.Runtime.CompilerServices;

namespace ModuleStartupTypeTrigger
{
    public static class Kernels
    {
        private static int initialized;

        static Kernels() { initialized = 7; }

        [ModuleInitializer]
        internal static void Initialize() { }

        public static int Read(int value) => initialized + value;
    }
}

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class ModuleInitializerAttribute : Attribute { }
}
