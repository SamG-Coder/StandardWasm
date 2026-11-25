# Runnable demos

Run npm ci once from the repository root. Run npm run demo to list available stages. Each stage builds its own .NET Standard DLL and compiles it to a standalone Wasm module. Use npm test for the cross-engine comparison with .NET.

| Stage | Run from repository root | Expected result |
| --- | --- | --- |
| [01 · Scalar methods](stage-01-scalars/README.md) | `npm run demo -- stage-01-scalars` | SumSquares(10) = 385 |
| [02 · Managed arrays](stage-02-managed-arrays/README.md) | `npm run demo -- stage-02-managed-arrays` | PrefixChecksum(2, 3) = 8 |
| [03 · Managed byte buffers](stage-03-byte-buffers/README.md) | `npm run demo -- stage-03-byte-buffers` | RoundTrip(3, 1, 511) = 1023 |
| [04 · PE metadata discovery](stage-04-pe-metadata/README.md) | `npm run demo -- stage-04-pe-metadata` | Inspect(PeLibrary.dll, 0) = 0 |
| [05 · CLI metadata streams](stage-05-metadata-streams/README.md) | `npm run demo -- stage-05-metadata-streams` | Inspect(StreamLibrary.dll, 32, -1) = 5 |
| [06 · Method definitions](stage-06-method-definitions/README.md) | `npm run demo -- stage-06-method-definitions` | HasMethods(MethodLibrary.dll) = 1 |
| [07 · Method bodies](stage-07-method-bodies/README.md) | `npm run demo -- stage-07-method-bodies` | InspectRow(BodyLibrary.dll, 1, -1) = 0 |
| [08 · Integer division boundaries](stage-08-integer-division/README.md) | `npm run demo -- stage-08-integer-division` | Divide(-7, 2) = -3 |
| [09 · Portable emitter](stage-09-portable-emitter/README.md) | `npm run demo -- stage-09-portable-emitter` | DemoKernel(11) = 40 |
| [10 · Portable control flow](stage-10-portable-control-flow/README.md) | `npm run demo -- stage-10-portable-control-flow` | Fold(6, 20) = 17 |
| [11 · Portable method linking](stage-11-portable-linking/README.md) | `npm run demo -- stage-11-portable-linking` | CombinePaths(12) = 96 |
| [18 · 64-bit integers](stage-18-long-integers/README.md) | `npm run demo -- stage-18-long-integers` | SumOfCubes(1000) = 250500250000 |
| [20 · Floating point](stage-20-floating-point/README.md) | `npm run demo -- stage-20-floating-point` | KahanSum(0.1, 10) = 1 |
| [25 · Switch dispatch](stage-25-switch-dispatch/README.md) | `npm run demo -- stage-25-switch-dispatch` | TokenizeSample(-1, 0) = 20203 |
| [29 · Math intrinsics](stage-29-math-intrinsics/README.md) | `npm run demo -- stage-29-math-intrinsics` | Hypotenuse(3, 4) = 5 |
| [33 · Floating-point remainder](stage-33-float-remainder/README.md) | `npm run demo -- stage-33-float-remainder` | WrapDegrees(-30) = 330 |
| [37 · Primitive arrays](stage-37-primitive-arrays/README.md) | `npm run demo -- stage-37-primitive-arrays` | CountPrimes(100) = 25 |

## Adding a stage

Choose a stable stage number in the tracking issue. Add a stage-NN-name directory containing its project, C# sources, demo.json and README.md. Give each assembly a unique name because it identifies both the artifact and the test module. Copy an existing manifest and specify the assembly, export type, method, inputs and expected value. Document the capability, command and expected result. Add the project reference to Verify/Verify.csproj and CLR comparison cases in Verify/Program.cs, then run the stage command and npm test. Update this index in the same PR. Keep generated files under ignored bin, obj and artifacts directories.
