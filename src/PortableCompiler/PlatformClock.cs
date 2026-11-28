using PeLibrary;
using static PeLibrary.ImageBytes;

namespace PortableCompiler
{
    // Decision 0104: System.Diagnostics.Stopwatch and the constant platform static fields that
    // benchmark-style console programs read. The browser host supplies a monotonic clock in
    // nanoseconds through `standardwasm:clock` `timestamp: () -> i64`, so Stopwatch.Frequency
    // is the constant 1_000_000_000 (the value .NET reports on Linux). Windows reports the QPC
    // rate instead; programs that convert ticks through Frequency observe the same durations.
    internal static class PlatformClock
    {
        internal const int Frequency = 1, IsHighResolution = 2, IsLittleEndian = 3;
        internal const long TicksPerSecond = 1000000000;

        private const string Module = "standardwasm:clock", Member = "timestamp";
        internal const int TypeBytes = 4, Capacity = 16;

        // A MemberRef row naming one of the constant platform static fields, or 0.
        internal static int StaticField(byte[] image, int[] heaps, int[] layout, int row)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, row, reference) < 0 ||
                reference[AssemblyReferences.Depth] != 0) return 0;
            int blob = reference[5];
            if (blob < 1 || !Fits((uint)blob, 3, (uint)heaps[1])) return 0;
            uint signature = (uint)(heaps[0] + blob);
            if (U16(image, signature) != 0x0602) return 0; // length 2, FIELD
            int element = image[signature + 2];
            int parent = reference[AssemblyReferences.ParentType];
            uint name = (uint)reference[4];
            if (SpanMetadata.IsPlatformType(image, layout, heaps, parent, "System.Diagnostics", "Stopwatch"))
            {
                if (element == 0x0a && MatchesAscii(image, heaps, name, "Frequency")) return Frequency;
                if (element == 0x02 && MatchesAscii(image, heaps, name, "IsHighResolution")) return IsHighResolution;
                return 0;
            }
            return element == 0x02 && MatchesAscii(image, heaps, name, "IsLittleEndian") &&
                SpanMetadata.IsPlatformType(image, layout, heaps, parent, "System", "BitConverter") ? IsLittleEndian : 0;
        }

        // static int64 System.Diagnostics.Stopwatch::GetTimestamp()
        internal static bool IsGetTimestamp(byte[] image, int[] heaps, int[] layout, int row)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, row, reference) < 0 ||
                reference[AssemblyReferences.Depth] != 0 ||
                !MatchesAscii(image, heaps, (uint)reference[4], "GetTimestamp")) return false;
            int blob = reference[5];
            // Length 3: default static, no parameters, returns Int64.
            if (blob < 1 || !Fits((uint)blob, 4, (uint)heaps[1]) ||
                U32(image, (uint)(heaps[0] + blob)) != 0x0a000003) return false;
            return SpanMetadata.IsPlatformType(image, layout, heaps, reference[AssemblyReferences.ParentType],
                "System.Diagnostics", "Stopwatch");
        }

        internal static int EntryBytes(int type) => Module.Length + Member.Length + 3 + WasmBinary.UnsignedSize(type);

        internal static int WriteType(byte[] output, int cursor)
        {
            output[cursor++] = 0x60;
            output[cursor++] = 0;
            output[cursor++] = 1;
            output[cursor++] = 0x7e; // monotonic nanoseconds
            return cursor;
        }

        internal static int WriteEntry(byte[] output, int cursor, int type)
        {
            cursor = Win32ErrorWasm.WriteName(output, cursor, Module);
            cursor = Win32ErrorWasm.WriteName(output, cursor, Member);
            output[cursor++] = 0;
            return WasmBinary.Unsigned(output, cursor, type);
        }
    }
}
