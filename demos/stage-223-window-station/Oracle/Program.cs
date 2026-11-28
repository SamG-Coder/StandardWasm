using System.Text.Json;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
const int systemSeed = 0xbeef, savedSeed = 0xcafe;
const uint neededSeed = 0x22222222;
var requests = new[]
{
    new Request("Flags", true, 1, 12, true, true, false),
    new Request("WithoutNeeded", true, 1, 12, true, false, false),
    new Request("Oversized", true, 1, 16, true, true, false),
    new Request("Scalar", true, 1, 12, true, true, true),
    new Request("Short", true, 1, 11, true, true, false),
    new Request("ZeroSize", true, 1, 0, true, true, false),
    new Request("LengthOnly", true, 1, 0, false, true, false),
    new Request("NullOutput", true, 1, 12, false, true, false),
    new Request("NullHandle", false, 1, 12, true, true, false),
    new Request("InvalidIndex", true, 0, 12, true, true, false),
    new Request("NegativeIndex", true, -1, 12, true, true, false),
    new Request("AliasedLength", true, 1, 12, true, true, false, true)
};

Guest.SeedErrors(systemSeed, savedSeed);
nint station = Guest.Station();
int system = Guest.SystemError(), saved = Guest.SavedError();
var identity = new { nonzero = station != 0, stable = Guest.Station() == station && Guest.ScalarStation() == station, system, saved };
var cases = new Dictionary<string, object>();
unsafe
{
    var output = stackalloc byte[16];
    foreach (Request request in requests)
    {
        var bytes = new Span<byte>(output, 16);
        bytes.Fill(0xa5);
        uint needed = neededSeed;
        nint handle = request.handle ? station : 0;
        nint data = request.output ? (nint)output : 0;
        nint length = request.alias ? (nint)(output + 8) : request.needed ? (nint)(&needed) : 0;
        Guest.SeedErrors(systemSeed, savedSeed);
        int ok = request.scalar ?
            Guest.ScalarFlags(handle, request.index, data, request.size, length) :
            Guest.Flags(handle, request.index, data, request.size, length);
        system = Guest.SystemError();
        saved = Guest.SavedError();
        cases.Add(request.name, new { ok, system, saved, needed, bytes = Convert.ToHexString(bytes) });
    }
}
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    fixture = typeof(Guest).Assembly.Location,
    systemSeed, savedSeed, neededSeed, identity, requests, cases
}));

record Request(string name, bool handle, int index, uint size, bool output, bool needed, bool scalar, bool alias = false);
