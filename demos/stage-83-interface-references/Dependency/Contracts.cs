namespace InterfaceContracts
{
    public interface IToken { }
    public interface IDerivedToken : IToken { }

    public interface IValue
    {
        int Read();
    }

    public class Token : IDerivedToken
    {
        public int Value;

        public Token(int value) => Value = value;
    }

    public class Holder
    {
        public IToken Stored;

        public Holder(IToken value) => Stored = value;
        public IToken Read() => Stored;
    }
}
