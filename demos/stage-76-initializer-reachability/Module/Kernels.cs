using System;
using System.Runtime.CompilerServices;

namespace ModuleInitFixture
{
    public static class Kernel
    {
        public static int Initialized;

        [ModuleInitializer]
        public static void Initialize() => Initialized = 1;

        public static int Read(int value) => value;
    }
}

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class ModuleInitializerAttribute : Attribute { }
}
