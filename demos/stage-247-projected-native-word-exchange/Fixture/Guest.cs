using System;
using System.Runtime.CompilerServices;
using System.Threading;

public static class Guest
{
    private struct Word
    {
        internal nint Token;
        internal nint Other;
        internal Word(nint token, nint other) { Token = token; Other = other; }
    }

    public static int Projected()
    {
        Word value = new Word(-13, 71);
        nint before = Interlocked.Exchange(ref value.Token, 29);
        return before == -13 && value.Token == 29 && value.Other == 71 ? 101 : 0;
    }

    public static int StableAlias()
    {
        Word value = new Word(23, 107);
        ref nint projected = ref value.Token;
        Word snapshot = value;
        value = new Word(37, 109);
        nint previous = Replace(ref projected, -97);
        return previous == 37 && projected == -97 && value.Token == -97 && value.Other == 109 &&
            snapshot.Token == 23 && snapshot.Other == 107 ? 103 : 0;
    }

    public static int Distinct()
    {
        Word first = new Word(11, 13), second = new Word(17, 19);
        ref nint a = ref first.Other;
        ref nint b = ref second.Token;
        nint previousA = Replace(ref a, 131);
        nint previousB = Replace(ref b, 137);
        a = -1;
        return previousA == 13 && previousB == 17 && first.Token == 11 && first.Other == -1 &&
            second.Token == 137 && second.Other == 19 ? 107 : 0;
    }

    public static int Frame()
    {
        nint word = int.MinValue;
        nint previous = Replace(ref word, int.MaxValue);
        return previous == int.MinValue && word == int.MaxValue ? 109 : 0;
    }

    public static int NullLocation()
    {
        try { Interlocked.Exchange(ref Unsafe.NullRef<nint>(), 9); }
        catch (NullReferenceException) { return 113; }
        return 0;
    }

    private static nint Replace(ref nint address, nint value) => Interlocked.Exchange(ref address, value);
}
