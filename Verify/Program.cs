using System.Text.Json;
using Sample;
using ArrayLibrary;

var cases = new List<object>();
void Check(string module, string method, int[] inputs, Func<int> run)
{
    int? expected;
    try { expected = run(); }
    catch (ArithmeticException) { expected = null; }
    cases.Add(new { module, method, inputs, expected });
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
File.WriteAllText(args.Single(), JsonSerializer.Serialize(cases));
