using System.Text.Json;
using Sample;
using ArrayLibrary;
using ByteLibrary;
using IntegerDivision;

if (args is ["--portable-audit", var request, var output])
{
    PortableAuditRows.Write(request, output);
    return;
}

var cases = new List<object>();
// Each case records the CLR result. expected == null means the CLR threw one of the exceptions below,
// and the Wasm module must trap. Wasm has no typed exceptions yet, so the exception type name is kept
// for reporting and is not compared with the engine's trap message.
void Check(string module, string method, int[] inputs, Func<int> run)
{
    int? expected;
    string exception = null;
    try { expected = run(); }
    catch (Exception e) when (e is ArithmeticException or IndexOutOfRangeException)
    {
        expected = null;
        exception = e.GetType().FullName;
    }
    cases.Add(new { module, method, inputs, expected, exception });
}

var random = new Random(6941);
int[] boundaries = [int.MinValue, -1, 0, 1, int.MaxValue];
for (int i = 0; i < 300; i++)
{
    int a = i < 25 ? boundaries[i % 5] : (int)random.NextInt64(int.MinValue, (long)int.MaxValue + 1);
    int b = i < 25 ? boundaries[i / 5] : random.Next(-200, 200);
    int n = random.Next(0, 512);
    Check("Sample", "Mix", [a, b], () => Kernels.Mix(a, b));
    Check("Sample", "Between", [a, b, n], () => Kernels.Between(a, b, n) ? 1 : 0);
    Check("Sample", "Gcd", [a, b], () => Kernels.Gcd(a, b));
    Check("Sample", "SumSquares", [n], () => Kernels.SumSquares(n));
    Check("ArrayLibrary", "PrefixChecksum", [n, a], () => Sequences.PrefixChecksum(n, a));
}
// Keep the CLR oracle's recursion shallow. A separate engine stress case checks deep Wasm tail calls.
Check("Sample", "Countdown", [300, 2], () => Kernels.Countdown(300, 2));
Check("ArrayLibrary", "PrefixChecksum", [-1, 0], () => Sequences.PrefixChecksum(-1, 0));
int[] byteValues = [int.MinValue, -257, -1, 0, 127, 128, 255, 256, 511, int.MaxValue];
foreach (int value in byteValues)
{
    Check("ByteLibrary", "Narrow", [value], () => Buffers.Narrow(value));
    foreach (int index in new[] { 0, 1, 2 })
        Check("ByteLibrary", "RoundTrip", [3, index, value], () => Buffers.RoundTrip(3, index, value));
}

foreach (var (count, index) in new[] { (1, 0), (3, 2), (0, 0), (3, -1), (3, 3), (-1, 0) })
{
    Check("ByteLibrary", "ZeroInitialized", [count, index], () => Buffers.ZeroInitialized(count, index));
    Check("ByteLibrary", "RoundTrip", [count, index, 255], () => Buffers.RoundTrip(count, index, 255));
}

PeCases.Add(cases);
StreamCases.Add(cases);
MethodCases.Add(cases);
BodyCases.Add(cases);
EmitterCases.Add(cases);
FlowCases.Add(cases);
LinkCases.Add(cases);
TypedArrayCases.Add(cases);
BootstrapCases.Add(cases);
ObjectCases.Add(cases);
DelegateCases.Add(cases);

// Integer division (stage 08). The grid crosses every signed boundary with every divisor boundary.
// The unsigned high-bit patterns (0x80000000, 0x80000001, 0xFFFFFFFE, 0xFFFFFFFF) are the same values
// read as int, so div.un and rem.un see dividends and divisors above int.MaxValue.
int[] divisionBoundaries =
[
    int.MinValue, int.MinValue + 1, -10, -8, -7, -2, -1, 0, 1, 2, 7, 8, 10, int.MaxValue - 1, int.MaxValue
];
void CheckDivision(int a, int b)
{
    Check("IntegerDivision", "Divide", [a, b], () => Quotients.Divide(a, b));
    Check("IntegerDivision", "Remainder", [a, b], () => Quotients.Remainder(a, b));
    Check("IntegerDivision", "DivideUnsigned", [a, b], () => Quotients.DivideUnsigned(a, b));
    Check("IntegerDivision", "RemainderUnsigned", [a, b], () => Quotients.RemainderUnsigned(a, b));
    Check("IntegerDivision", "QuotientIdentityHolds", [a, b], () => Quotients.QuotientIdentityHolds(a, b) ? 1 : 0);
}
void CheckConstantDivisors(int a)
{
    Check("IntegerDivision", "DivideByNegativeOne", [a], () => Quotients.DivideByNegativeOne(a));
    Check("IntegerDivision", "RemainderByNegativeOne", [a], () => Quotients.RemainderByNegativeOne(a));
    Check("IntegerDivision", "DivideByEight", [a], () => Quotients.DivideByEight(a));
    Check("IntegerDivision", "RemainderBySeven", [a], () => Quotients.RemainderBySeven(a));
    Check("IntegerDivision", "DivideUnsignedByTen", [a], () => Quotients.DivideUnsignedByTen(a));
    Check("IntegerDivision", "UnsignedDigitSum", [a], () => Quotients.UnsignedDigitSum(a));
}
foreach (int a in divisionBoundaries)
{
    foreach (int b in divisionBoundaries)
        CheckDivision(a, b);
    CheckConstantDivisors(a);
}
var divisionRandom = new Random(3301);
for (int i = 0; i < 200; i++)
{
    int a = (int)divisionRandom.NextInt64(int.MinValue, (long)int.MaxValue + 1);
    // Alternate small divisors, which give large quotients, with full-range divisors, which give
    // mostly zero or small quotients. The small range includes zero.
    int b = i % 2 == 0 ? divisionRandom.Next(-50, 51)
                       : (int)divisionRandom.NextInt64(int.MinValue, (long)int.MaxValue + 1);
    CheckDivision(a, b);
    CheckConstantDivisors(a);
}
LongCases.Add(cases);
FloatCases.Add(cases);
SwitchCases.Add(cases);
RemainderCases.Add(cases);
MathCases.Add(cases);
ArrayCases.Add(cases);
CultureProbe.Write(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args.Single()))!, "culture-probe", "MathIntrinsics.dll"));
File.WriteAllText(args.Single(), JsonSerializer.Serialize(cases));
