using System;
namespace GeneralDelegateFixture
{
    public sealed class Scale
    {
        public int Factor;
        public int Apply(int value) => value * Factor;
    }
    public delegate long Combine(long a, int b);
    public delegate void Sink(string s);
    public delegate Scale Factory(int factor);
    public delegate int Chain(Chain next, int x);
    public sealed class Holder { public Func<int, int> Map; }
    public static class Kernels
    {
        static Func<int, int> stored;
        static int Twice(int x) => x * 2;
        static int Length(string s) => s.Length;
        static object Same(object o) => o;
        static int seen;
        static void Record(int x) => seen += x;
        static long Mix(long a, int b) => a * 10 + b;
        static int Apply(Func<int, int> f, int x) => f(x);

        public static int StaticFuncIntInt() { Func<int, int> f = Twice; return f(21); }
        public static int StaticFuncStringInt() { Func<string, int> f = Length; return f("hello"); }
        public static int ObjectIdentity() { Func<object, object> f = Same; var o = new object(); return ReferenceEquals(f(o), o) ? 1 : 0; }
        public static int ActionInt() { seen = 0; Action<int> a = Record; a(5); a(7); return seen; }
        public static int InstanceTarget() { var s = new Scale { Factor = 3 }; Func<int, int> f = s.Apply; return f(14); }
        public static int CapturedLambda() { int k = 4; Func<int, int> f = v => v + k; return f(38); }
        public static int CachedLambda() => Apply(v => v - 1, 43);
        public static int PassedAndStored() { stored = Twice; var h = new Holder { Map = stored }; return Apply(h.Map, 10) + stored(1); }
        public static int TwoParameters() { Func<long, int, long> f = Mix; return (int)f(4, 2); }
        public static int NullInvoke() { Func<int, int> f = null; try { return f(1); } catch (NullReferenceException) { return 9; } }
        static string sunk;
        static void Keep(string s) => sunk = s;
        static Scale Make(int f) => new Scale { Factor = f };
        public static int CustomStatic() { Combine c = Mix; return (int)c(4, 2); }
        public static int CustomVoid() { Sink k = Keep; k("abc"); return sunk.Length; }
        public static int CustomInstance() { var s = new Scale { Factor = 5 }; Combine c = (a, b) => a * s.Factor + b; return (int)c(8, 2); }
        public static int CustomClassResult() { Factory f = Make; return f(6).Apply(7); }
        public static int CustomNull() { Combine c = null; try { return (int)c(1, 1); } catch (NullReferenceException) { return 9; } }
        public static int RejectSelfReference() { Chain c = (n, x) => x + 1; return c(c, 41); }
        public static int RejectCast() { object o = (Combine)Mix; return (int)((Combine)o)(4, 2); }
        public static int RejectMulticast() { Combine c = Mix; c += Mix; return (int)c(4, 2); }
        public static int RejectIsType() { object o = (Combine)Mix; return o is Sink ? 1 : 2; }
        public static int RejectFuncCast() { object o = (Func<int, int>)Twice; return ((Func<int, int>)o)(21); }
        public static int DoubleResult() { Func<double, double> f = d => d * 1.5; return (int)(f(10.0) * 10); }
    }
}
