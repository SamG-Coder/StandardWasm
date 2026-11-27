using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Packed16References;

public static unsafe class Probe
{
    public static int SignedReadAndWrite()
    {
        char[] buffer = ['L', '\u8001', 'R'];
        var suffix = new Span<char>(buffer).Slice(1);
        ref char original = ref MemoryMarshal.GetReference(suffix);
        ref short signed = ref Unsafe.As<char, short>(ref original);
        int before = Read(signed: ref signed);
        Write(ref signed, (short)-2);
        return before == -32767 && buffer[0] == 'L' && buffer[1] == '\ufffe' && buffer[2] == 'R' ? 171 : 0;
    }

    public static int ReverseView()
    {
        char[] buffer = ['a', 'b'];
        ref char source = ref MemoryMarshal.GetReference(new Span<char>(buffer));
        ref short signed = ref Unsafe.As<char, short>(ref source);
        ref char original = ref Unsafe.As<short, char>(ref signed);
        original = '\uffff';
        return signed == -1 && buffer[0] == '\uffff' && buffer[1] == 'b' ? 173 : 0;
    }

    public static int NativeBacked()
    {
        char* storage = stackalloc char[2];
        storage[0] = '\u8000';
        storage[1] = 'X';
        ref char source = ref MemoryMarshal.GetReference(new Span<char>(storage, 2));
        ref short signed = ref Unsafe.As<char, short>(ref source);
        int before = signed;
        signed = -1;
        return before == -32768 && storage[0] == '\uffff' && storage[1] == 'X' ? 177 : 0;
    }

    public static int NullAlias()
    {
        ref char source = ref Unsafe.NullRef<char>();
        ref short signed = ref Unsafe.As<char, short>(ref source);
        if (!Unsafe.IsNullRef(ref signed)) return 0;
        try { return signed; }
        catch (NullReferenceException) { return 179; }
    }

    private static T Read<T>(ref T signed) where T : unmanaged => signed;
    private static void Write<T>(ref T signed, T value) where T : unmanaged => signed = value;
}
