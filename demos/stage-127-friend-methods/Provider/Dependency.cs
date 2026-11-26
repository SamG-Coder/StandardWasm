namespace FriendFixture
{
#if PUBLIC_MEMBERS
    // The wrong-key caller compiles against public members with the same identity,
    // then runs against the signed provider whose members are internal.
    public static class Dependency
    {
        public static int Compute(int input) => input + 9;
        public static int Visible(int input) => input + 1;
    }

    public class Widener
    {
        public static int Wide(int input) => input * 3;
    }

    public sealed class Box
    {
        public readonly int Value;
        public Box(int value) { Value = value; }
        public int Twice() => Value * 2;
    }
#else
    public static class Dependency
    {
        // Assembly access: visible to InternalsVisibleTo friends.
        internal static int Compute(int input) => input + 9;

        // A public member of the same type binds for every caller, granted or not.
        public static int Visible(int input) => input + 1;

        // Private stays inaccessible even to friends.
        private static int Hidden(int input) => input - 1;
    }

    public class Widener
    {
        // FamORAssem (protected internal): a friend that is not a subclass reaches it
        // through the assembly part.
        protected internal static int Wide(int input) => input * 3;
    }

    public sealed class Box
    {
        public readonly int Value;
        internal Box(int value) { Value = value; }
        internal int Twice() => Value * 2;
    }
#endif

    public class ProtectedBase
    {
        protected virtual int Transform(int input) => input + 11;
        protected int Scale(int input) => input * 3;
    }
}
