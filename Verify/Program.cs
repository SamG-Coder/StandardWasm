using Sample;
using ArrayLibrary;
using ByteLibrary;

if (args is ["--native-storage-oracle", var nativeOracle])
{
    NativeStorageCases.Write32BitOracle(nativeOracle);
    return;
}

var cases = new List<object>();
void Check(string module, string method, int[] inputs, Func<int> run)
{
    int? expected;
    try { expected = run(); }
    catch (ArithmeticException) { expected = null; }
    catch (IndexOutOfRangeException) { expected = null; }
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
ControlStorageCases.Add(cases);
StringCases.Add(cases);
FormattingCases.Add(cases);
DynamicControlsCases.Add(cases);
BundleCases.Add(cases);
PortableWideCases.Add(cases);
PortableSwitchCases.Add(cases);
DiagnosticCases.Add(cases);
PlatformIdentityCases.Add(cases);
WideStorageCases.Add(cases);
PortableFloatCases.Add(cases);
ResizeCases.Add(cases);
TextBoxCases.Add(cases);
SharedWinFormsCases.Add(cases);
ReferenceFieldCases.Add(cases);
SenderIdentityCases.Add(cases);
PortablePrimitiveCases.Add(cases);
PortableNarrowScalarCases.Add(cases);
ArrayLiteralCases.Add(cases);
CheckedIntegerCases.Add(cases);
CheckedCastCases.Add(cases);
TypedOverflowCases.Add(cases);
ExceptionTableCases.Add(cases);
OverflowHandlerCases.Add(cases);
ArithmeticHandlerCases.Add(cases);
NestedHandlerCases.Add(cases);
RethrowCases.Add(cases);
ArrayBoundsCases.Add(cases);
NullAccessCases.Add(cases);
FinallyCases.Add(cases);
AllocationDelegateCases.Add(cases);
InstanceNullCases.Add(cases);
LinkedExceptionCases.Add(cases);
LinkedReferenceCases.Add(cases);
LinkedObjectCases.Add(cases);
LinkedNominalCases.Add(cases);
LinkedInstanceCases.Add(cases);
NullStackCases.Add(cases);
LinkedFieldCases.Add(cases);
ForeignFieldTypeCases.Add(cases);
NominalCastCases.Add(cases);
BaseExceptionCases.Add(cases);
NullThrowCases.Add(cases);
BareCatchCases.Add(cases);
InheritedLayoutCases.Add(cases);
InitializerReachabilityCases.Add(cases);
StrongNameCases.Add(cases);
ForwardingCases.Add(cases);
StaticLayoutCases.Add(cases);
InterfaceReferenceCases.Add(cases);
NativeStorageCases.Add(cases);
NestedReferenceCases.Add(cases);
GenericLayoutCases.Add(cases);
PackedFieldCases.Add(cases);
VolatileFieldCases.Add(cases);
PointerFieldCases.Add(cases);
EmbeddedValueCases.Add(cases);
EnumFieldCases.Add(cases);
TypedPointerCases.Add(cases);
FloatFieldCases.Add(cases);
PrimitiveGenericCases.Add(cases);
ByRefPermissionCases.Add(cases);
ValueConstraintCases.Add(cases);
NamedValueCases.Add(cases);
ExplicitScalarCases.Add(cases);
NestedOverlayCases.Add(cases);
EnumArgumentCases.Add(cases);
HandleConstraintCases.Add(cases);
WideLayoutCases.Add(cases);
StaticStorageCases.Add(cases);
BeforeFieldInitCases.Add(cases);
OracleWriter.Write(args.Single(), cases);
