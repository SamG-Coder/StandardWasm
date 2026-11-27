using System;

namespace ImmutableBytePins;

public static unsafe class Probe
{
    public static int First()
    {
        ReadOnlySpan<byte> view = "Wasm"u8;
        fixed (byte* data = view)
            return *data;
    }

    public static int LastAndTerminator()
    {
        ReadOnlySpan<byte> view = "Wasm"u8;
        fixed (byte* data = view)
            return data[3] * 10 + data[4];
    }

    public static int SeparateConsumer()
    {
        ReadOnlySpan<byte> view = "Wasm"u8;
        fixed (byte* data = view)
            return Read(data);
    }

    private static int Read(byte* data) => data[0];
}
