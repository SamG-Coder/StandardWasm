namespace FriendFixture
{
    // Compiled against a provider that grants (or exposes) these members; the runner then
    // bundles it with the provider under test. The CLR enforces the same friend rules.
    public static class Caller
    {
        // internal static, protected internal static from a non-subclass, and an
        // internal constructor plus internal instance method on another assembly's type.
        public static int Run(int input) => Dependency.Compute(input) + Widener.Wide(input) + new Box(input).Twice();

        public static int ComputeOnly(int input) => Dependency.Compute(input);

        // A public call into a type that also declares internal members. It must bind
        // even when the provider grants nothing, without consulting friend metadata.
        public static int PublicOnly(int input) => Dependency.Visible(input);

        public static int ProtectedOnly(int input) => new Derived().FromBase(input);
    }

    public sealed class Derived : ProtectedBase
    {
        protected override int Transform(int input) => input - 10;

        public int FromBase(int input) => base.Transform(input) + this.Scale(input);
    }
}
