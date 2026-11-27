using StringIdentityDependency;

namespace StringIdentityFixture
{
    public static class Kernels
    {
        public static string Local() => "é\0東京𐐀";
        public static string SameLiteral() => "é\0東京𐐀";
        public static string CrossAssembly() => Literals.Shared();
        public static string DifferentLiteral() => "é\0東京𐐁";
        public static string Empty() => "";
        public static string SameEmpty() => "";
        public static string EmptyField() => string.Empty;
        public static string EmptyConstructor() => new string((char[])null);
        public static string EmptyConcat() => string.Concat((string)null, "");
        public static string ConcatLeft() => string.Concat("", Local());
        public static string ConcatRight() => string.Concat(Local(), (string)null);
        public static string ConcatNonempty() => string.Concat(Local(), DifferentLiteral());
        public static object ObjectReturn() => Echo(Local());
        public static string ObjectCast() => Cast(Echo(Literals.Shared()));
        public static string ObjectTest() => Test(Echo(Local()));
        public static string NullCast() => Cast(Echo(null));
        public static string WrongTest() => Test(new object());

        public static string WrongCast()
        {
            try { return Cast(new object()); }
            catch (System.InvalidCastException) { return DifferentLiteral(); }
        }

        public static string ManagedIdentity() =>
            Echo(Local()) == Echo(Literals.Shared()) ? Local() : DifferentLiteral();

        private static object Echo(object value) => value;
        private static string Cast(object value) => (string)value;
        private static string Test(object value) => value as string;

        public static string Dynamic() => new string(new[] { 'é', '\0', '東', '京', '\ud801', '\udc00' });
    }
}
