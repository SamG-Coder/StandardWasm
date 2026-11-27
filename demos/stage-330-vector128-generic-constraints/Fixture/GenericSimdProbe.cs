using System;
using System.Runtime.Intrinsics;

public static class GenericSimdProbe
{
    public static int ValueConstraint() => PassValue(Vector128.Create((ushort)40000)).GetElement(3);
    public static int InterfaceConstraint() => PassEquatable(Vector128.Create((ushort)45000)).GetElement(7);
    public static int ExplicitClosedConstraint() => PassVectorEquatable(Vector128.Create((ushort)65000)).GetElement(0);
    public static int PrimitiveControl() => PassEquatable(93);

    private static T PassValue<T>(T value) where T : struct => value;
    private static T PassEquatable<T>(T value) where T : struct, IEquatable<T> => value;
    private static T PassVectorEquatable<T>(T value) where T : IEquatable<Vector128<ushort>> => value;
}
