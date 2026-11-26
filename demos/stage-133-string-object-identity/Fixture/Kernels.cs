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
        public static string Dynamic() => new string(new[] { 'é', '\0', '東', '京', '\ud801', '\udc00' });
    }
}
