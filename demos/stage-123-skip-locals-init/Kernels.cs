namespace System.Runtime.CompilerServices
{
    // netstandard2.0 does not ship this attribute. The C# compiler recognizes it by name
    // and clears CorILMethod_InitLocals on every method it covers.
    [AttributeUsage(AttributeTargets.Module | AttributeTargets.Class | AttributeTargets.Struct |
        AttributeTargets.Constructor | AttributeTargets.Method, Inherited = false)]
    internal sealed class SkipLocalsInitAttribute : Attribute { }
}

namespace SkipLocalsInitFixture
{
    using System.Runtime.CompilerServices;

    public struct Pair
    {
        public int Left;
        public int Right;
    }

    public sealed class Box
    {
        public int Value;
    }

    // Every method here has locals and no InitLocals flag. C# only reads definitely assigned
    // locals, so each result is fully determined even though the CLR leaves the slots unspecified.
    [SkipLocalsInit]
    public static class Kernels
    {
        // Scalar locals, assigned on every path before use.
        public static int Scalars(int x)
        {
            int a;
            long b;
            if (x > 0) { a = x * 3; b = a + 1L; }
            else { a = -x; b = 7; }
            return a + (int)b;
        }

        // A value-type local written field by field, then copied: the copy stays independent.
        public static int Value(int x)
        {
            Pair p;
            p.Left = x;
            p.Right = x * 2;
            Pair q = p;
            q.Left = 1;
            return p.Left + p.Right + q.Left;
        }

        // Locals declared in nested blocks inside a loop.
        public static int Nested(int x)
        {
            int total = 0;
            for (int i = 0; i < x; i++)
            {
                int square;
                {
                    square = i * i;
                }
                total += square;
            }
            return total;
        }

        // Reference locals: one assigned on every path, one starting null.
        public static int Objects(int x)
        {
            Box box;
            Box other = null;
            box = new Box();
            box.Value = x;
            if (x > 10) other = box;
            return other == null ? -box.Value : other.Value;
        }

        // stackalloc memory is not a Wasm local. It stays an explicit diagnostic (decision 0051).
        public static unsafe int Stack(int x)
        {
            int* p = stackalloc int[4];
            p[0] = x;
            return p[0];
        }
    }

    public static class Demo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] data, int index, int value) => data[index] = unchecked((byte)value);
        public static int ReadByte(byte[] data, int index) => data[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int Compile(byte[] image, int row, byte[] output) =>
            PortableCompiler.MethodCompiler.Compile(image, row, output);
    }
}
