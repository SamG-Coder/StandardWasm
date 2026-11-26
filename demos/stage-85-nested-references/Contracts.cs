using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("NestedDemo, PublicKey=0024000004800000140100000602000000240000525341310008000001000100059F4055E156DEFAC76171F1923BE0CFDF57D78B38D19748CBEADC02499DA223BBAD57A206AAE73FE6FE240E1020547C8477DF703A2849EA3FB231AE609E9393C22C722C248EEC6EC7C20DD83356D23D2433E53A9EB8A0FA2383108A3B0C239FED278F692846769473112936E2D5D1507C53C9AC535EA04D6BDA48DF62FE9779224C35313FBEFCB52192C5957DF5CCE4B1DB3ADFDCC917BC9A608DBD2C354D472EABF384D1B9A4E5EE79121D037AA038528F7DFE041961BDE674CB3B98E93164CCB9F3569DB5FF8EC66883D3111F4B4287716AE1D240C0BCA9671CCFAE58A49EFF3B631383A6A643001B04517C5656643CA68A3D9DCDF47A818E89071F60FEBE")]

namespace NestedContract
{
    public static class Left
    {
        public interface Marker { }

        public static class Branch
        {
            public sealed class Token : Marker
            {
                public int Value;

                public Token(int value) => Value = value;
            }
        }
    }

    public static class Right
    {
        public interface Marker { }

        public static class Branch
        {
            public sealed class Token : Marker
            {
                public int Value;

                public Token(int value) => Value = value;
            }
        }
    }

    public sealed class Holder
    {
        public Left.Marker Value;
    }

    internal static class Hidden
    {
        internal interface Marker { }

        internal sealed class Token : Marker
        {
            public int Value;

            public Token(int value) => Value = value;
        }
    }
}
