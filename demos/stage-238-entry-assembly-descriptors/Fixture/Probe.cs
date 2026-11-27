using System.Reflection;
using System.Runtime.CompilerServices;

namespace EntryAssemblyDependency
{
    public static class Probe
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Assembly Entry() => Assembly.GetEntryAssembly();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Assembly Executing() => Assembly.GetExecutingAssembly();
    }
}
