using System.Reflection;
using System.Runtime.CompilerServices;

namespace ExecutingDependency
{
    public static class Imported
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Assembly Current() => Assembly.GetExecutingAssembly();
    }
}
