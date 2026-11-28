using System;
using System.Reflection;

public static class Probe
{
    public static int DescriptorCells()
    {
        Type[] values = new Type[] { typeof(int), typeof(string) };
        return values.Length == 2 && values[0] == typeof(int) &&
            values[1] == typeof(string) ? 101 : 0;
    }

    public static int SharedIdentity()
    {
        Type[] descriptors = new Type[] { typeof(int) };
        Type[] nominal = new Type[1];
        return RuntimeIdentity(descriptors) == typeof(Type[]) && RuntimeIdentity(nominal) == typeof(Type[]) &&
            RuntimeIdentity(new object[1]) == typeof(object[]) ? 103 : 0;
    }

    public static int NullCell()
    {
        Type[] values = new Type[] { typeof(string), null };
        return values[1] is null && values[0] == typeof(string) ? 107 : 0;
    }

    public static int Bounds()
    {
        Type[] values = new Type[] { typeof(string) };
        try
        {
            return values[1].Name.Length;
        }
        catch (IndexOutOfRangeException)
        {
            return 109;
        }
    }

    public static int NominalPath() => ReadNominal(null);
    public static int MixedPath() => ReadMixed(null);

    public static int AddressedLocal()
    {
        Type[] values = new Type[] { typeof(int) };
        Replace(ref values);
        return values is null ? 131 : 0;
    }

    private static Type RuntimeIdentity(object value) => value.GetType();
    private static void Replace<T>(ref T value) => value = default(T);

    private static int ReadNominal(TypeDelegator nominal)
    {
        Type[] values = new Type[1];
        values[0] = nominal;
        return values.Length == 1 ? 113 : 0;
    }

    private static int ReadMixed(TypeDelegator nominal)
    {
        Type[] values = new Type[] { typeof(int), nominal };
        return values.Length == 2 ? 127 : 0;
    }
}
