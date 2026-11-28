using System;
using System.Reflection;
using Dependency = EntryAssemblyDependency.Probe;

public static class ProgramProbe
{
    public static int DefaultEntry()
    {
        var entry = Assembly.GetEntryAssembly();
        return entry is not null && ReferenceEquals(entry, Assembly.GetExecutingAssembly()) &&
            ReferenceEquals(entry, Dependency.Entry()) && !ReferenceEquals(entry, Dependency.Executing()) ? 941 : 0;
    }

    public static int OverrideAndRestore()
    {
        var original = Assembly.GetEntryAssembly();
        var dependency = Dependency.Executing();
        Assembly.SetEntryAssembly(dependency);
        bool changed = ReferenceEquals(dependency, Assembly.GetEntryAssembly()) && ReferenceEquals(dependency, Dependency.Entry());
        Assembly.SetEntryAssembly(original);
        return changed && ReferenceEquals(original, Assembly.GetEntryAssembly()) ? 947 : 0;
    }

    public static int ExplicitNull()
    {
        var original = Assembly.GetEntryAssembly();
        Assembly.SetEntryAssembly(null);
        bool isNull = Assembly.GetEntryAssembly() is null && Dependency.Entry() is null;
        Assembly.SetEntryAssembly(original);
        return isNull && ReferenceEquals(original, Assembly.GetEntryAssembly()) ? 953 : 0;
    }
}
