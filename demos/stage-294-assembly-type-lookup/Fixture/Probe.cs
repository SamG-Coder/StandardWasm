extern alias Other;
using System;
using System.Reflection;
using OtherCandidate = Other::AssemblyTypeNames.Candidate;

namespace AssemblyTypeNames;

public sealed class Candidate
{
    static Candidate() => Probe.Initialized++;
}

public sealed class Open<T>;

public static class Probe
{
    internal static int Initialized;

    private sealed class Nested;

    private static Type Lookup(Assembly assembly, string name) => assembly.GetType(name, false, false);

    private static string SelectName(bool present) =>
        present ? " \tAssemblyTypeNames.Candidate" : "AssemblyTypeNames.Unknown";

    public static int DynamicName()
    {
        Assembly assembly = typeof(Probe).Assembly;
        string name = SelectName(Initialized == 0);
        return Lookup(assembly, name) == typeof(Candidate) && Initialized == 0 ? 101 : -1;
    }

    public static int ScopedIdentity()
    {
        const string Name = "AssemblyTypeNames.Candidate";
        Type own = Lookup(typeof(Probe).Assembly, Name);
        Type other = Lookup(typeof(OtherCandidate).Assembly, Name);
        return own == typeof(Candidate) && other == typeof(OtherCandidate) && own != other ? 103 : -1;
    }

    public static int DefinitionNames()
    {
        Assembly assembly = typeof(Probe).Assembly;
        Type nested = Lookup(assembly, "AssemblyTypeNames.Probe+Nested");
        Type open = Lookup(assembly, "AssemblyTypeNames.Open`1");
        return nested == typeof(Nested) && open == typeof(Open<>) ? 107 : -1;
    }

    public static int MissingNames()
    {
        Assembly assembly = typeof(Probe).Assembly;
        return Lookup(assembly, "assemblytypenames.Candidate") == null &&
            Lookup(assembly, SelectName(false)) == null &&
            Lookup(typeof(OtherCandidate).Assembly, "AssemblyTypeNames.Probe") == null ? 109 : -1;
    }

    public static int CoreIdentity()
    {
        Assembly assembly = typeof(object).Assembly;
        return Lookup(assembly, "System.Object") == typeof(object) &&
            Lookup(assembly, "System.Int32") == typeof(int) ? 113 : -1;
    }

    public static int NullAndEmpty()
    {
        Assembly assembly = typeof(Probe).Assembly;
        int result = 0;
        try { Lookup(assembly, null); }
        catch (ArgumentNullException) { result += 41; }
        try { Lookup(assembly, ""); }
        catch (ArgumentException) { result += 80; }
        return result;
    }

    public static int CaseFlagOutsideProfile() =>
        typeof(Probe).Assembly.GetType("assemblytypenames.Candidate", false, true) == null ? -1 : 127;
}
