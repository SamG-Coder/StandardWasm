using System.Reflection;

public static class Probe
{
    public static int Default() => typeof(Probe).Assembly.GetName().Name == "GetNameProbe" ? 101 : -1;

    public static int WithFlag() => typeof(Probe).Assembly.GetName(true).Name == "GetNameProbe" ? 103 : -1;

    public static int Version() => typeof(Probe).Assembly.GetName().Version.Major == 1 ? 107 : -1;
    public static int ManagedIdentity()
    {
        var first = new AssemblyName();
        var second = new AssemblyName();
        first.Name = "First";
        second.Name = "Second";
        return first.Name == "First" && second.Name == "Second" && first != second ? 109 : -1;
    }

    private sealed class Custom : Assembly
    {
        public override AssemblyName GetName(bool copiedName)
        {
            var name = new AssemblyName();
            name.Name = copiedName ? "CustomCopy" : "CustomOriginal";
            return name;
        }
    }

    public static int CustomSlot()
    {
        Assembly value = new Custom();
        return value.GetName(true).Name == "CustomCopy" ? 113 : -1;
    }
}
