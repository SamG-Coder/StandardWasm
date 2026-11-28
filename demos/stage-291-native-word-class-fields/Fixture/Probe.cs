using System;
using System.Threading;

namespace NativeWordClassFields
{
    public static class Probe
    {
        private class Box<T>
        {
            internal nint Token;
            internal nint Other;
            internal Box(nint token, nint other) { Token = token; Other = other; }
        }

        private sealed class Derived : Box<int>
        {
            internal Derived(nint token, nint other) : base(token, other) { }
        }

        public static int Exchange()
        {
            var box = new Box<int>(-13, 71);
            nint before = Interlocked.Exchange(ref box.Token, (nint)29);
            return before == -13 && box.Token == 29 && box.Other == 71 ? 101 : 0;
        }

        public static int StableAlias()
        {
            var box = new Box<int>(23, 107);
            ref nint address = ref box.Token;
            box.Token = 37;
            nint before = Replace(ref address, -97);
            return before == 37 && address == -97 && box.Token == -97 && box.Other == 107 ? 103 : 0;
        }

        public static int DistinctOwners()
        {
            var first = new Box<int>(11, 13);
            var second = new Box<int>(17, 19);
            ref nint a = ref first.Other;
            ref nint b = ref second.Token;
            nint beforeA = Replace(ref a, 131);
            nint beforeB = Replace(ref b, 137);
            a = -1;
            return beforeA == 13 && beforeB == 17 && first.Token == 11 &&
                first.Other == -1 && second.Token == 137 && second.Other == 19 ? 107 : 0;
        }

        public static int InheritedField()
        {
            var box = new Derived(59, 61);
            nint before = Replace(ref box.Token, 127);
            return before == 59 && box.Token == 127 && box.Other == 61 ? 109 : 0;
        }

        public static int NullFieldOwner()
        {
            try { return Replace(ref ((Box<int>)null).Token, 9) == 0 ? 0 : 0; }
            catch (NullReferenceException) { return 113; }
        }

        private static nint Replace(ref nint address, nint value) => Interlocked.Exchange(ref address, value);
    }
}
