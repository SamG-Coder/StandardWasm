# Runnable demos

Run npm ci once from the repository root. Run npm run demo to list available stages. Compiler stages build a .NET Standard DLL and run its Wasm output. Use npm test for compiler parity and npm run test:winforms for the Windows and browser application check.

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
| [12 · Portable managed arrays and self-recompilation](stage-12-portable-arrays/README.md) | `npm run demo -- stage-12-portable-arrays` | Route(255, 3) = 516 |
| [13 · Complete compiler bootstrap](stage-13-complete-bootstrap/README.md) | `npm run demo -- stage-13-complete-bootstrap` | BootstrapKernel(21) = 43 |
| [14 · WinForms on WebGPU](stage-14-winforms-webgpu/README.md) | `npm run winforms` | Click and Enter update the counter |
| [15 · Managed instances](stage-15-managed-instances/README.md) | `npm run demo -- stage-15-managed-instances` | Isolated(10) = 19 |
| [16 · Managed delegate binding](stage-16-managed-delegates/README.md) | `npm run demo -- stage-16-managed-delegates` | Route(10, 1) = 12 |
| [17 · Dense WebGPU redraws](stage-17-dense-webgpu/README.md) | `npm run bench:webgpu` | Timing sweep and screenshot parity |
| [18 · 64-bit integers](stage-18-long-integers/README.md) | `npm run demo -- stage-18-long-integers` | SumOfCubes(1000) = 250500250000 |
| [19 · Runtime controls](stage-19-runtime-controls/README.md) | `npm run demo -- stage-19-runtime-controls` | Change(511) = 766 |
| [20 · Managed UTF-16 strings](stage-20-managed-strings/README.md) | `npm run demo -- stage-20-managed-strings` | CodeUnit(0, 3) = 55357 |
| [20 · Floating point](stage-20-floating-point/README.md) | `npm run demo -- stage-20-floating-point` | KahanSum(0.1, 10) = 1 |
| [21 · Managed label formatting](stage-21-managed-formatting/README.md) | `npm run demo -- stage-21-managed-formatting` | Unit(-2147483648, 0) = 45 |
| [22 · Dynamic controls](stage-22-dynamic-controls/README.md) | `npm run demo -- stage-22-dynamic-controls` | Toggle(1) = 356 |
| [23 · Linking managed assemblies](stage-23-assembly-linking/README.md) | `npm run demo -- stage-23-assembly-linking` | Calculate(9) = 89 |
| [25 · Switch dispatch](stage-25-switch-dispatch/README.md) | `npm run demo -- stage-25-switch-dispatch` | TokenizeSample(-1, 0) = 20203 |
| [29 · Math intrinsics](stage-29-math-intrinsics/README.md) | `npm run demo -- stage-29-math-intrinsics` | Hypotenuse(3, 4) = 5 |
| [33 · Floating-point remainder](stage-33-float-remainder/README.md) | `npm run demo -- stage-33-float-remainder` | WrapDegrees(-30) = 330 |
| [37 · Primitive arrays](stage-37-primitive-arrays/README.md) | `npm run demo -- stage-37-primitive-arrays` | CountPrimes(100) = 25 |
| [72 · Windows reference executable](stage-72-winforms-windows-reference/README.md) | `npm run demo -- stage-72-winforms-windows-reference` | Both .NET 10 apphosts launch, with interaction and dependency evidence |
| [73 · Windows executable intake](stage-73-executable-intake/README.md) | `npm run demo -- stage-73-executable-intake` | Node and Edge recover the original managed entry and manifests |
| [74 · Run a dropped .NET entry point](stage-74-executable-entry/README.md) | `npm run demo -- stage-74-executable-entry` | Original console publishes run through the generated compiler |
| [77 · Pointer memory-model experiment](stage-77-pointer-memory-model/README.md) | `npm run demo -- stage-77-pointer-memory-model` | Model A fails 5 aliasing cases; models B and C match all 34 CLR cases in Node and Edge |
| [79 · Modal message-loop experiment](stage-79-modal-message-loop/README.md) | `npm run demo -- stage-79-modal-message-loop` | JSPI (Node, Edge) and worker (Edge) traces match the Windows WinForms dispatch order |
| [95 � Original managed companions](stage-95-managed-companions/README.md) | `npm run demo -- stage-95-managed-companions` | Edge drops and runs original multi-assembly console publishes |

## Adding a stage

Choose a stable stage number in the tracking issue. Add a stage-NN-name directory containing its project, C# sources, demo.json and README.md. Give each assembly a unique name because it identifies both the artifact and the test module. Copy an existing manifest and specify the assembly, export type, method, inputs and expected value. Document the capability, command and expected result. Add the project reference to Verify/Verify.csproj and CLR comparison cases in Verify/Program.cs, then run the stage command and npm test. Update this index in the same PR. Keep generated files under ignored bin, obj and artifacts directories.

Browser application stages can declare a runner in demo.json. Give them a documented verification command for the native and browser hosts.
