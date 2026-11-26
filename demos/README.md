# Runnable demos

Run npm ci once from the repository root. Run npm run demo to list available stages. Compiler stages build a .NET Standard DLL and run its Wasm output. Use npm test for compiler parity and npm run test:winforms for the Windows and browser application check.

| Stage | Run from repository root | Expected result |
| --- | --- | --- |
| [01 · Scalar methods](stage-01-scalars/README.md) | `npm run demo -- stage-01-scalars` | SumSquares(10) = 385 |
| [02 · Managed arrays](stage-02-managed-arrays/README.md) | `npm run demo -- stage-02-managed-arrays` | PrefixChecksum(2, 3) = 8 |
| [03 � Managed byte buffers](stage-03-byte-buffers/README.md) | `npm run demo -- stage-03-byte-buffers` | RoundTrip(3, 1, 511) = 1023 |
| [04 � PE metadata discovery](stage-04-pe-metadata/README.md) | `npm run demo -- stage-04-pe-metadata` | Inspect(PeLibrary.dll, 0) = 0 |
| [05 � CLI metadata streams](stage-05-metadata-streams/README.md) | `npm run demo -- stage-05-metadata-streams` | Inspect(StreamLibrary.dll, 32, -1) = 5 |

| [06 · Method definitions](stage-06-method-definitions/README.md) | `npm run demo -- stage-06-method-definitions` | HasMethods(MethodLibrary.dll) = 1 |
| [07 · Method bodies](stage-07-method-bodies/README.md) | `npm run demo -- stage-07-method-bodies` | InspectRow(BodyLibrary.dll, 1, -1) = 0 |
| [09 · Portable emitter](stage-09-portable-emitter/README.md) | `npm run demo -- stage-09-portable-emitter` | DemoKernel(11) = 40 |
| [10 · Portable control flow](stage-10-portable-control-flow/README.md) | `npm run demo -- stage-10-portable-control-flow` | Fold(6, 20) = 17 |
| [11 · Portable method linking](stage-11-portable-linking/README.md) | `npm run demo -- stage-11-portable-linking` | CombinePaths(12) = 96 |
| [12 · Portable managed arrays and self-recompilation](stage-12-portable-arrays/README.md) | `npm run demo -- stage-12-portable-arrays` | Route(255, 3) = 516 |
| [13 · Complete compiler bootstrap](stage-13-complete-bootstrap/README.md) | `npm run demo -- stage-13-complete-bootstrap` | BootstrapKernel(21) = 43 |
| [14 · WinForms on WebGPU](stage-14-winforms-webgpu/README.md) | `npm run winforms` | Click and Enter update the counter |
| [15 · Managed instances](stage-15-managed-instances/README.md) | `npm run demo -- stage-15-managed-instances` | Isolated(10) = 19 |
| [16 · Managed delegate binding](stage-16-managed-delegates/README.md) | `npm run demo -- stage-16-managed-delegates` | Route(10, 1) = 12 |
| [17 · Dense WebGPU redraws](stage-17-dense-webgpu/README.md) | `npm run bench:webgpu` | Timing sweep and screenshot parity |
| [19 · Runtime controls](stage-19-runtime-controls/README.md) | `npm run demo -- stage-19-runtime-controls` | Change(511) = 766 |
| [20 · Managed UTF-16 strings](stage-20-managed-strings/README.md) | `npm run demo -- stage-20-managed-strings` | CodeUnit(0, 3) = 55357 |
| [21 · Managed label formatting](stage-21-managed-formatting/README.md) | `npm run demo -- stage-21-managed-formatting` | Unit(-2147483648, 0) = 45 |
| [22 · Dynamic controls](stage-22-dynamic-controls/README.md) | `npm run demo -- stage-22-dynamic-controls` | Toggle(1) = 356 |
| [23 · Linking managed assemblies](stage-23-assembly-linking/README.md) | `npm run demo -- stage-23-assembly-linking` | Calculate(9) = 89 |
| [24 · Portable 64-bit integers](stage-24-portable-i64/README.md) | `npm run demo -- stage-24-portable-i64` | Calculate(9007199254740993, 1) = 95496328098598384 |
| [26 · Portable switch dispatch](stage-26-portable-switch/README.md) | `npm run demo -- stage-26-portable-switch` | Classify(-3) = 17 |
| [27 · Portable compiler diagnostics](stage-27-portable-diagnostics/README.md) | `npm run demo -- stage-27-portable-diagnostics` | BoxedValue yields -2 at IL_0001 |
| [28 · Managed 64-bit storage](stage-28-wide-storage/README.md) | `npm run demo -- stage-28-wide-storage` | Apply(9007199254740993, 1, 3) = 36028797018963972 |
| [30 · Portable floating point](stage-30-portable-floats/README.md) | `npm run demo -- stage-30-portable-floats` | Filter(9, 3) = 8 |
| [31 · Resizable WinForms layout](stage-31-resize-winforms/README.md) | `npm run demo -- stage-31-resize-winforms` | Additional button X after resizing = 264 |
| [32 · WinForms text entry](stage-32-winforms-textbox/README.md) | `npm run demo -- stage-32-winforms-textbox` | Editing Name updates the WebGPU greeting |
| [34 · Shared WinForms application source](stage-34-shared-winforms-source/README.md) | `npm run demo -- stage-34-shared-winforms-source` | The same C# initializes the Profile controls on Windows and in Wasm |
| [35 · Managed WinForms control tree](stage-35-managed-control-tree/README.md) | `npm run demo -- stage-35-managed-control-tree` | Chain(41) = 41 through managed references |
| [36 · Shared WinForms event subscriptions](stage-36-managed-events/README.md) | `npm run demo -- stage-36-managed-events` | SubscriptionOrder() = 12112 through an ordinary C# event |
| [38 · Profile rendering timings](stage-38-profile-rendering/README.md) | `npm run demo -- stage-38-profile-rendering` | Same-device timing and WebGPU/Canvas 2D pixel comparison |
| [39 · Portable primitive array storage](stage-39-portable-primitive-arrays/README.md) | `npm run demo -- stage-39-portable-primitive-arrays` | ReadSigned(-32769) = 32767 |
| [41 · Portable narrow scalar signatures](stage-41-portable-narrow-scalars/README.md) | `npm run demo -- stage-41-portable-narrow-scalars` | ReadSigned(255, 32768) = -32769 |
| [43 · Primitive array literals](stage-43-array-literals/README.md) | `npm run demo -- stage-43-array-literals` | IntegerAt(1) = -93 |
| [44 · Portable checked integers](stage-44-checked-integers/README.md) | `npm run demo -- stage-44-checked-integers` | MulI32(-12, 7) = -84 |
| [45 · Checked fixed-width numeric casts](stage-45-checked-casts/README.md) | `npm run demo -- stage-45-checked-casts` | F64ToU8(255.9) = 255 |
| [46 · Typed checked overflow](stage-46-typed-overflow/README.md) | `npm run demo -- stage-46-typed-overflow` | AddOrDivide raises the OverflowException tag |
| [47 · CIL exception tables](stage-47-exception-tables/README.md) | `npm run demo -- stage-47-exception-tables` | The DLL reader reports one exception clause for SmallCatch |
| [48 · Managed overflow handlers](stage-48-overflow-catch/README.md) | `npm run demo -- stage-48-overflow-catch` | Increment catches overflow and returns -1 |
| [49 · Ordered arithmetic catches](stage-49-arithmetic-catches/README.md) | `npm run demo -- stage-49-arithmetic-catches` | SpecificThenBase(18, 0) = -101 |
| [50 · Nested arithmetic catches](stage-50-nested-catches/README.md) | `npm run demo -- stage-50-nested-catches` | HandlerEscapesToOuter(int.MaxValue, 0) = -201 |
| [51 · Arithmetic exception rethrow](stage-51-arithmetic-rethrow/README.md) | `npm run demo -- stage-51-arithmetic-rethrow` | RetainOuter(int.MaxValue, 0) = -101 |
| [52 · Typed array bounds exceptions](stage-52-array-bounds-exceptions/README.md) | `npm run demo -- stage-52-array-bounds-exceptions` | Rethrow(0, -1) = -202 |
| [53 · Typed null reference exceptions](stage-53-typed-null-access/README.md) | `npm run demo -- stage-53-typed-null-access` | ReadArray(0, -1) = -102 |
| [54 · Nested finally and exceptional cleanup](stage-54-finally/README.md) | `npm run demo -- stage-54-finally` | CatchAfterCleanup(0) = 1234 |
| [55 · Allocation and delegate exceptions](stage-55-allocation-delegates/README.md) | `npm run demo -- stage-55-allocation-delegates` | AllocationCleanup(-1) = -17 |
| [56 · Typed null failures in instance calls](stage-56-instance-null/README.md) | `npm run demo -- stage-56-instance-null` | CallCleanup(0, 2) = -101 |
| [57 · Linked exception regions](stage-57-linked-exceptions/README.md) | `npm run demo -- stage-57-linked-exceptions` | Run(18, 0, 4) = 116 |
| [58 · Linked arrays and strings](stage-58-linked-references/README.md) | `npm run demo -- stage-58-linked-references` | Alias(0, 7) = 797 |
| [59 · Assembly-scoped object layouts](stage-59-linked-objects/README.md) | `npm run demo -- stage-59-linked-objects` | Run(5) = 47 |
| [60 · Class signatures across DLLs](stage-60-nominal-linking/README.md) | `npm run demo -- stage-60-nominal-linking` | Run(5, 3) = 325 |
| [61 · Constructors and instance calls across DLLs](stage-61-linked-instances/README.md) | `npm run demo -- stage-61-linked-instances` | Run(5, 3) = 1110 |
| [62 · Null references at branch joins](stage-62-null-stack/README.md) | `npm run demo -- stage-62-null-stack` | Loop(4) = 20 |
| [63 · Instance fields across DLLs](stage-63-linked-fields/README.md) | `npm run demo -- stage-63-linked-fields` | Run(5) = 33 |
| [64 · Fields with types declared in another DLL](stage-64-foreign-field-types/README.md) | `npm run demo -- stage-64-foreign-field-types` | Run(17) = 35 |
| [65 · Managed reference casts](stage-65-nominal-casts/README.md) | `npm run demo -- stage-65-nominal-casts` | Run(7) = 13 |
| [66 · Managed exception base catches](stage-66-base-exception-catches/README.md) | `npm run demo -- stage-66-base-exception-catches` | Rethrow runs cleanup and returns 118 |
| [67 · Explicit null throws](stage-67-null-throw/README.md) | `npm run demo -- stage-67-null-throw` | A typed null exception runs cleanup and returns 113 |

## Adding a stage

Choose a stable stage number in the tracking issue. Add a stage-NN-name directory containing its project, C# sources, demo.json and README.md. Give each assembly a unique name because it identifies both the artifact and the test module. Copy an existing manifest and specify the assembly, export type, method, inputs and expected value. Document the capability, command and expected result. Add the project reference to Verify/Verify.csproj and CLR comparison cases in Verify/Program.cs, then run the stage command and npm test. Update this index in the same PR. Keep generated files under ignored bin, obj and artifacts directories.

Browser application stages can declare a runner in demo.json. Give them a documented verification command for the native and browser hosts.
