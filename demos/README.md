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
| [24 · Portable 64-bit integers](stage-24-portable-i64/README.md) | `npm run demo -- stage-24-portable-i64` | Calculate(9007199254740993, 1) = 95496328098598384 |
| [25 · Switch dispatch](stage-25-switch-dispatch/README.md) | `npm run demo -- stage-25-switch-dispatch` | TokenizeSample(-1, 0) = 20203 |
| [26 · Portable switch dispatch](stage-26-portable-switch/README.md) | `npm run demo -- stage-26-portable-switch` | Classify(-3) = 17 |
| [27 · Portable compiler diagnostics](stage-27-portable-diagnostics/README.md) | `npm run demo -- stage-27-portable-diagnostics` | Resolve a located diagnostic for an unsupported framework call |
| [28 · Managed 64-bit storage](stage-28-wide-storage/README.md) | `npm run demo -- stage-28-wide-storage` | Apply(9007199254740993, 1, 3) = 36028797018963972 |
| [29 · Math intrinsics](stage-29-math-intrinsics/README.md) | `npm run demo -- stage-29-math-intrinsics` | Hypotenuse(3, 4) = 5 |
| [30 · Portable floating point](stage-30-portable-floats/README.md) | `npm run demo -- stage-30-portable-floats` | Filter(9, 3) = 8 |
| [31 · Resizable WinForms layout](stage-31-resize-winforms/README.md) | `npm run demo -- stage-31-resize-winforms` | Additional button X after resizing = 264 |
| [32 · WinForms text entry](stage-32-winforms-textbox/README.md) | `npm run demo -- stage-32-winforms-textbox` | Editing Name updates the WebGPU greeting |
| [33 · Floating-point remainder](stage-33-float-remainder/README.md) | `npm run demo -- stage-33-float-remainder` | WrapDegrees(-30) = 330 |
| [34 · Shared WinForms application source](stage-34-shared-winforms-source/README.md) | `npm run demo -- stage-34-shared-winforms-source` | The same C# initializes the Profile controls on Windows and in Wasm |
| [35 · Managed WinForms control tree](stage-35-managed-control-tree/README.md) | `npm run demo -- stage-35-managed-control-tree` | Chain(41) = 41 through managed references |
| [36 · Shared WinForms event subscriptions](stage-36-managed-events/README.md) | `npm run demo -- stage-36-managed-events` | SubscriptionOrder() = 12112 through an ordinary C# event |
| [37 · Primitive arrays](stage-37-primitive-arrays/README.md) | `npm run demo -- stage-37-primitive-arrays` | CountPrimes(100) = 25 |
| [38 · Profile rendering timings](stage-38-profile-rendering/README.md) | `npm run demo -- stage-38-profile-rendering` | Same-device timing and WebGPU/Canvas 2D pixel comparison |
| [39 · Portable primitive array storage](stage-39-portable-primitive-arrays/README.md) | `npm run demo -- stage-39-portable-primitive-arrays` | ReadSigned(-32769) = 32767 |
| [40 · Portable Math intrinsics](stage-40-portable-math/README.md) | `npm run demo -- stage-40-portable-math` | Compile(PortableMath.dll, Hypotenuse) = 5 |
| [41 · Portable narrow scalar signatures](stage-41-portable-narrow-scalars/README.md) | `npm run demo -- stage-41-portable-narrow-scalars` | ReadSigned(255, 32768) = -32769 |
| [42 · Generate the Profile in the browser](stage-42-generated-profile/README.md) | `npm run demo -- stage-42-generated-profile` | Generated compiler emits Profile in Edge, then the WebGPU form matches Windows interaction |
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
| [68 · Bare catch clauses](stage-68-bare-catch/README.md) | `npm run demo -- stage-68-bare-catch` | A bare catch rethrows to a typed handler and returns -301 |
| [69 · Profile WebGPU text uploads](stage-69-profile-text-uploads/README.md) | `npm run demo -- stage-69-profile-text-uploads` | Partial and full text uploads produce identical pixels |
| [70 · Profile preview redraw](stage-70-profile-caption-redraw/README.md) | `npm run demo -- stage-70-profile-caption-redraw` | Only the preview caption is rasterized on an edit |
| [72 · Windows reference executable](stage-72-winforms-windows-reference/README.md) | `npm run demo -- stage-72-winforms-windows-reference` | Both .NET 10 apphosts launch, with interaction and dependency evidence |
| [73 · Windows executable intake](stage-73-executable-intake/README.md) | `npm run demo -- stage-73-executable-intake` | Node and Edge recover the original managed entry and manifests |
| [74 · Run a dropped .NET entry point](stage-74-executable-entry/README.md) | `npm run demo -- stage-74-executable-entry` | Original console publishes run through the generated compiler |
| [75 · Inherited layouts](stage-75-inherited-layouts/README.md) | `npm run demo -- stage-75-inherited-layouts` | Base and derived field storage are shared across three DLLs |
| [76 · Initializer reachability](stage-76-initializer-reachability/README.md) | `npm run demo -- stage-76-initializer-reachability` | Unused type initializers leave independent calls available |
| [77 · Pointer memory-model experiment](stage-77-pointer-memory-model/README.md) | `npm run demo -- stage-77-pointer-memory-model` | Model A fails 5 aliasing cases; models B and C match all 34 CLR cases in Node and Edge |
| [78 · Strong-named assembly identity](stage-78-strong-names/README.md) | `npm run demo -- stage-78-strong-names` | A compiled DLL binds a signed dependency by its public key token |
| [79 · Modal message-loop experiment](stage-79-modal-message-loop/README.md) | `npm run demo -- stage-79-modal-message-loop` | JSPI (Node, Edge) and worker (Edge) traces match the Windows WinForms dispatch order |
| [80 · CLR type forwarding](stage-80-type-forwarding/README.md) | `npm run demo -- stage-80-type-forwarding` | An unchanged caller follows a signed type facade to its implementation |
| [81 · CoreLib object root](stage-81-corelib-object-root/README.md) | `npm run demo -- stage-81-corelib-object-root` | The installed CoreLib constructor chain agrees with the CLR |
| [82 · Static fields and instance layout](stage-82-static-field-layout/README.md) | `npm run demo -- stage-82-static-field-layout` | Static fields do not shift inherited instance storage |
| [83 · Interface references](stage-83-interface-references/README.md) | `npm run demo -- stage-83-interface-references` | Linked interface fields preserve reference identity and null |
| [84 · Native-sized storage](stage-84-native-sized-storage/README.md) | `npm run demo -- stage-84-native-sized-storage` | Linked native-sized fields use wasm32 width |
| [85 · Nested type references](stage-85-nested-references/README.md) | `npm run demo -- stage-85-nested-references` | Nested identities survive cross-DLL binding and forwarding |
| [86 · Closed generic layouts](stage-86-closed-generic-layouts/README.md) | `npm run demo -- stage-86-closed-generic-layouts` | Closed reference arguments specialize instance and array storage |
| [87 · Packed integer instance fields](stage-87-packed-integer-fields/README.md) | `npm run demo -- stage-87-packed-integer-fields` | Packed field loads preserve CLI width and signedness |
| [88 · Payload memory-pressure experiment](stage-88-payload-pressure/README.md) | `npm run demo -- stage-88-payload-pressure` | Model C strategies lower peaks but none holds 32 MiB; model B's collector holds about 33 MiB |
| [89 · Unmanaged null pointer fields](stage-89-unmanaged-pointer-fields/README.md) | `npm run demo -- stage-89-unmanaged-pointer-fields` | Linked void pointer fields preserve safe null flows |
| [90 · Volatile fields](stage-90-volatile-field-signatures/README.md) | `npm run demo -- stage-90-volatile-field-signatures` | Linked volatile instance and static fields retain metadata identity and single-agent order |
| [91 · Embedded sequential value types](stage-91-embedded-value-types/README.md) | `npm run demo -- stage-91-embedded-value-types` | Embedded values retain defaults and independent copies |
| [92 · Nominal enum fields](stage-92-nominal-enums/README.md) | `npm run demo -- stage-92-nominal-enums` | Linked enum identity preserves backing width and flags behavior |
| [93 � Typed null pointer fields](stage-93-typed-pointer-fields/README.md) | `npm run demo -- stage-93-typed-pointer-fields` | Pointer levels and CLR pointee identity survive linked null storage |
| [94 � Floating-point instance fields](stage-94-floating-point-fields/README.md) | `npm run demo -- stage-94-floating-point-fields` | Linked Single and Double fields preserve storage precision |
| [95 � Original managed companions](stage-95-managed-companions/README.md) | `npm run demo -- stage-95-managed-companions` | Edge drops and runs original multi-assembly console publishes |
| [96 � Primitive generic layouts](stage-96-primitive-generic-layouts/README.md) | `npm run demo -- stage-96-primitive-generic-layouts` | Closed scalar and vector arguments retain CLI identity through linked value copies |
| [97 � Generic byref-like permission](stage-97-byref-like-permission/README.md) | `npm run demo -- stage-97-byref-like-permission` | Ordinary arguments bind to .NET 10 generic definitions with AllowByRefLike |
| [98 � Non-nullable value constraints](stage-98-nonnullable-value-constraints/README.md) | `npm run demo -- stage-98-nonnullable-value-constraints` | Closed struct constraints and original CoreLib Nullable field layout |
| [99 � Named value arguments](stage-99-named-value-arguments/README.md) | `npm run demo -- stage-99-named-value-arguments` | Closed generics with linked sequential struct arguments |
| [100 � Explicit scalar layouts](stage-100-explicit-scalar-layouts/README.md) | `npm run demo -- stage-100-explicit-scalar-layouts` | Shared bytes for overlapping scalar fields |
| [101 � Nested overlay views](stage-101-nested-overlay-views/README.md) | `npm run demo -- stage-101-nested-overlay-views` | Nested scalar value views of the same explicit bytes |
| [102 � Nominal enum arguments](stage-102-nominal-enum-arguments/README.md) | `npm run demo -- stage-102-nominal-enum-arguments` | Closed generic layouts retain enum identity and scalar storage |
| [103 � Handle generic constraints](stage-103-handle-generic-constraints/README.md) | `npm run demo -- stage-103-handle-generic-constraints` | Compiled unmanaged marker and closed nominal interface constraints |
| [104 � Wide layout IDs](stage-104-wide-layout-ids/README.md) | `npm run demo -- stage-104-wide-layout-ids` | Allocation, fields and checked casts over 300 managed layouts |
| [105 · Generic method bodies](stage-105-generic-method-bodies/README.md) | `npm run demo -- stage-105-generic-method-bodies` | Run(-128) = -122103 with distinct bodies per closed declaring type |
| [106 � Static field storage](stage-106-static-field-storage/README.md) | `npm run demo -- stage-106-static-field-storage` | Private typed Wasm globals shared by linked managed methods |
| [107 � Framework facades](stage-107-framework-facades/README.md) | `npm run demo -- stage-107-framework-facades` | Run original .NET Standard libraries through the runtime facade |
| [108 � BeforeFieldInit calls](stage-108-beforefieldinit-calls/README.md) | `npm run demo -- stage-108-beforefieldinit-calls` | Allow non-triggering static calls and diagnose reached initializers |
| [109 · Virtual dispatch on sealed receivers](stage-109-exact-virtual-dispatch/README.md) | `npm run demo -- stage-109-exact-virtual-dispatch` | Resolve inherited and hidden virtual slots for sealed classes |
| [110 · Console arguments and streams](stage-110-console-arguments/README.md) | `npm run demo -- stage-110-console-arguments` | Windows, Node and Edge match on exit status and stdout/stderr bytes for `Main(string[])` |
| [111 · Value-type construction](stage-111-value-construction/README.md) | `npm run demo -- stage-111-value-construction` | Local and linked value constructors, in place and through newobj, match the CLR |
| [112 · Standalone Webcil intake](stage-112-standalone-webcil-intake/README.md) | `npm run demo -- stage-112-standalone-webcil-intake` | Inspect the original .NET 10 standalone publish in Node and Edge |
| [113 · CoreLib native width](stage-113-corelib-native-width/README.md) | `npm run demo -- stage-113-corelib-native-width` | x86 and browser-wasm CoreLib IL matches the x86 CLR; x64 CoreLib bodies return -7 |
| [114 · Empty module startup](stage-114-empty-module-startup/README.md) | `npm run demo -- stage-114-empty-module-startup` | Prove empty module startup in standalone and linked .NET Standard libraries |
| [115 · Sequential reference layouts](stage-115-sequential-reference-layouts/README.md) | `npm run demo -- stage-115-sequential-reference-layouts` | Compile sequential classes like the browser CoreLib `Delegate` and `Type`; diagnose explicit, packed and auto-based shapes |
| [116 · Native integer vectors](stage-116-native-integer-vectors/README.md) | `npm run demo -- stage-116-native-integer-vectors` | wasm32 native vector storage, loads and stores on original managed DLLs |
| [117 · WinForms blocker census](stage-117-winforms-census/README.md) | `npm run demo -- stage-117-winforms-census` | Rank the compiler blockers and P/Invokes statically reached from the original WinForms entry |
| [118 · Bundle image reuse](stage-118-bundle-image-reuse/README.md) | `npm run demo -- stage-118-bundle-image-reuse` | Measure unchanged WinForms compilation in Node and Edge |
| [119 · Framework delegates](stage-119-framework-delegates/README.md) | `npm run demo -- stage-119-framework-delegates` | EventHandler subscribe, combine and remove through System.Runtime match the CLR |
| [120 · Virtual calls on newly allocated objects](stage-120-allocated-virtual-receivers/README.md) | `npm run demo -- stage-120-allocated-virtual-receivers` | Bind unsealed virtual receivers when the evaluation stack proves their class |
| [121 · Read-only value-type instance methods](stage-121-value-instance-methods/README.md) | `npm run demo -- stage-121-value-instance-methods` | Call non-mutating struct methods on locals and receivers; diagnose writes through `this` |
| [122 · Runtime class dispatch](stage-122-runtime-class-dispatch/README.md) | `npm run demo -- stage-122-runtime-class-dispatch` | Select the original virtual override for a polymorphic managed receiver |
| [123 · SkipLocalsInit bodies](stage-123-skip-locals-init/README.md) | `npm run demo -- stage-123-skip-locals-init` | `[SkipLocalsInit]` methods with locals compile with zeroed locals; stackalloc stays diagnosed |
| [124 · Closed generic bases](stage-124-closed-generic-bases/README.md) | `npm run demo -- stage-124-closed-generic-bases` | Substitute and reserve closed generic base classes from original managed DLLs |
| [125 · Runtime type handles](stage-125-runtime-type-handles/README.md) | `npm run demo -- stage-125-runtime-type-handles` | Compare canonical TypeDef and TypeRef identities from managed DLLs |
| [126 · Managed exception objects](stage-126-managed-exception-objects/README.md) | `npm run demo -- stage-126-managed-exception-objects` | Throw and catch linked exception classes against the x86 CLR; filters stay diagnosed |
| [127 · Friend-assembly methods](stage-127-friend-methods/README.md) | `npm run demo -- stage-127-friend-methods` | Bind internal and protected internal methods through InternalsVisibleTo; ungranted calls stay diagnosed |
| [128 · Interface dispatch](stage-128-interface-dispatch/README.md) | `npm run demo -- stage-128-interface-dispatch` | Inherited and explicit interface slots select their CLR bodies |
| [129 · Type initializers and root objects](stage-129-type-initializers/README.md) | `npm run demo -- stage-129-type-initializers` | Reached `.cctor`s run once at CLI triggers and cache supported managed failures per closed type |
| [130 · Fieldless sequential values](stage-130-fieldless-values/README.md) | `npm run demo -- stage-130-fieldless-values` | One-byte nominal empty values keep their managed identity |
| [131 · Runtime reflection](stage-131-runtime-reflection/README.md) | `npm run demo -- stage-131-runtime-reflection` | Preserve module identity and original basenames in compiled assemblies |
| [132 · Primitive unmanaged pointers](stage-132-unmanaged-pointers/README.md) | `npm run demo -- stage-132-unmanaged-pointers` | `byte*`, `int*` and other primitive pointers read and write host-lent memory; null raises NullReferenceException |
| [133 · String object identity](stage-133-string-object-identity/README.md) | `npm run demo -- stage-133-string-object-identity` | Preserve identity for identical literals across linked assemblies |
| [134 · Static aggregate fields](stage-134-static-aggregate-fields/README.md) | `npm run demo -- stage-134-static-aggregate-fields` | Read and write static structs with CLR copy and initializer semantics |
| [135 · Addressed value arguments](stage-135-addressed-value-arguments/README.md) | `npm run demo -- stage-135-addressed-value-arguments` | Preserve the callee copy when taking the address of a by-value struct argument |
| [136 · Closed generic methods](stage-136-method-instantiations/README.md) | `npm run demo -- stage-136-method-instantiations` | Verify MethodSpec metadata and execute closed static generic methods |
| [137 · stackalloc on a shadow stack](stage-137-stackalloc/README.md) | `npm run demo -- stage-137-stackalloc` | `stackalloc` into primitive pointers; frames release on return and exceptions; exhaustion traps |
| [138 · Inline-array layouts](stage-138-inline-array-layouts/README.md) | `npm run demo -- stage-138-inline-array-layouts` | Preserve the declared element and value copies of managed inline arrays |
| [139 · Managed object byrefs](stage-139-managed-object-byrefs/README.md) | `npm run demo -- stage-139-managed-object-byrefs` | A traced vector element alias survives calls and exceptional unwinding |
| [140 · Inline object addresses](stage-140-inline-object-addresses/README.md) | `npm run demo -- stage-140-inline-object-addresses` | A managed alias sees whole-value writes to its inline-array local |
| [141 · Read-only object spans](stage-141-object-readonly-spans/README.md) | `npm run demo -- stage-141-object-readonly-spans` | Retain the live inline storage owner and match CLR bounds checks |
| [142 · Read-only character spans](stage-142-character-readonly-spans/README.md) | `npm run demo -- stage-142-character-readonly-spans` | View the original string payload with CLR UTF-16 indexing |
| [143 · Struct pointers and sizeof](stage-143-struct-pointers/README.md) | `npm run demo -- stage-143-struct-pointers` | 32-bit CLR `sizeof`; `p->F`, `&p->F` and `p[i]` on blittable sequential structs in memory 0 |
| [144 · Writable character spans](stage-144-writable-character-spans/README.md) | `npm run demo -- stage-144-writable-character-spans` | Write into the traced managed UTF-16 array through a span alias |
| [145 · Struct copies through pointers](stage-145-struct-copies/README.md) | `npm run demo -- stage-145-struct-copies` | `Rect r = *p`, `*p = r`, `*p = default` and nested struct copies between memory 0 and managed values |
| [146 · Class value-field receivers](stage-146-class-value-receivers/README.md) | `npm run demo -- stage-146-class-value-receivers` | Call original managed methods through stable class-field addresses |
| [147 · Constructor receiver aliases](stage-147-constructor-receiver-aliases/README.md) | `npm run demo -- stage-147-constructor-receiver-aliases` | Preserve chained receiver writes in the original Windows Forms Padding constructor |
| [148 · Executing assembly identity](stage-148-executing-assembly/README.md) | `npm run demo -- stage-148-executing-assembly` | Resolve the defining assembly of the executing method across linked DLLs |
| [149 · Fixed strings](stage-149-fixed-strings/README.md) | `npm run demo -- stage-149-fixed-strings` | `fixed (char* p = s)` in the netstandard2.0 and net10.0 IL forms, read through a frame-owned UTF-16 copy |
| [150 · Function pointers](stage-150-function-pointers/README.md) | `npm run demo -- stage-150-function-pointers` | `delegate*<...>` through `ldftn` and `calli`: a funcref table slot and `call_indirect` |
| [151 · Interactive standard input](stage-151-interactive-stdin/README.md) | `npm run demo -- stage-151-interactive-stdin` | an unmodified console publish calling `Console.ReadLine`: stdin in Node, and lines typed into a responsive Edge page |
| [152 · Finalization root tracing](stage-152-finalization-root-tracing/README.md) | `npm run demo -- stage-152-finalization-root-tracing` | Trace explicit roots and retain finalizer receivers using Wasm GC references |
| [153 · Missing dependencies](stage-153-missing-dependencies/README.md) | `npm run demo -- stage-153-missing-dependencies` | a referenced DLL absent at run time: methods that need it throw CoreLib's `FileNotFoundException` as on the CLR (opt-in `CompileBundleDeferred`) |
| [154 · CoreLib resource strings](stage-154-corelib-resource-strings/README.md) | `npm run demo -- stage-154-corelib-resource-strings` | CoreLib `SR` resource strings folded at compile time, so default-message framework exceptions compile and match the x86 CLR |
| [155 · Bound catch variables](stage-155-bound-catch-variables/README.md) | `npm run demo -- stage-155-bound-catch-variables` | `catch (X e)` handlers reading the caught exception, including runtime-raised CoreLib exceptions and rethrow identity, matched with the x86 CLR in Node and Edge |
| [156 · Native imports](stage-156-native-imports/README.md) | `npm run demo -- stage-156-native-imports` | blittable `[DllImport]`s into kernel32 and ucrtbase, SetLastError and a missing entry point as typed Wasm imports, matched with the x86 CLR in Node and Edge |
| [157 · Boxed primitive interface dispatch](stage-157-boxed-primitive-interface-dispatch/README.md) | `npm run demo -- stage-157-boxed-primitive-interface-dispatch` | Match primitive boxes to CoreLib interface contracts and execute the original boxed `Char.TryFormat` body in Node and Edge |
| [158 · Managed interior field aliases](stage-158-managed-interior-field-aliases/README.md) | `npm run demo -- stage-158-managed-interior-field-aliases --no-rebuild` | Compare escaping field references, nested value replacement, and copy isolation across Windows CLR, Node, and Edge |
| [159 · Routed COM calls](stage-159-routed-com-calls/README.md) | `npm run demo -- stage-159-routed-com-calls --no-rebuild` | Compare a pointer-based stdcall vtable call and its HRESULT on Windows, Node, and Edge |
| [160 · Native receiver demand](stage-160-native-receiver-demand/README.md) | `npm run demo -- stage-160-native-receiver-demand --no-rebuild` | Verify native-only and mixed receiver bodies, including closed generic calls, on Windows, Node, and Edge |
| [161 · Aggregate boxes](stage-161-aggregate-boxes/README.md) | `npm run demo -- stage-161-aggregate-boxes --no-rebuild` | Verify boxed struct identity, copies, type tests, and typed failures |
| [162 · Closed reference casts](stage-162-closed-reference-casts/README.md) | `npm run demo -- stage-162-closed-reference-casts --no-rebuild` | Verify casts and type tests on constructed classes and interfaces |
| [163 · Class Int32 field aliases and exchange](stage-163-class-int32-exchange/README.md) | `npm run demo -- stage-163-class-int32-exchange` | Compare interior aliases and signed Int32 exchange across class, array, local, static and struct fields |
| [164 · Delegate representation](stage-164-delegate-representation/README.md) | `npm run demo -- stage-164-delegate-representation` | C# cached lambdas and open static delegate targets, matched with the x86 CLR in Node and Edge |
| [165 · Dynamic native calls](stage-165-dynamic-native-calls/README.md) | `npm run demo -- stage-165-dynamic-native-calls` | Compare typed calls through resolved export tokens, UTF-16 writes and diagnostics in Node and Edge |
| [166 · Span field reader arguments](stage-166-span-field-reader-arguments/README.md) | `npm run demo -- stage-166-span-field-reader-arguments` | Verify indexed and sliced span fields with ordinary argument helpers and a located diagnostic for a mutating helper |
| [167 · Closed generic sizeof](stage-167-closed-generic-sizeof/README.md) | `npm run demo -- stage-167-closed-generic-sizeof` | Compare nominal CLI sizes for closed method and declaring type arguments |
| [168 · Packed 16-bit managed references](stage-168-packed16-managed-references/README.md) | `npm run demo -- stage-168-packed16-managed-references` | Compare aliased character and signed short reads, writes and nulls in Node and Edge |
| [169 · Computed byte span indices](stage-169-computed-byte-span-indices/README.md) | `npm run demo -- stage-169-computed-byte-span-indices` | Compare byte-span checks, sliced aliases, and straight-line indices across CLR, Node, and Edge |
| [170 · Character span copying](stage-170-character-span-copy/README.md) | `npm run demo -- stage-170-character-span-copy` | Run original CoreLib span concatenation and compare overlapping and mixed-storage copies |
| [171 · Immutable byte pins](stage-171-immutable-byte-pins/README.md) | `npm run demo -- stage-171-immutable-byte-pins` | Compare fixed literal byte pointers and their stored terminator in Node and Edge |
| [172 · Nested private members](stage-172-nested-private-members/README.md) | `npm run demo -- stage-172-nested-private-members` | Compare closed generic private field and method binding from nested types |
| [173 · Managed array clearing](stage-173-array-clear/README.md) | `npm run demo -- stage-173-array-clear` | Clear verified vectors while preserving live scalar, reference, and aggregate aliases, with CLR failure parity |
| [174 · Inline reference-array constructors](stage-174-inline-reference-array-constructors/README.md) | `npm run demo -- stage-174-inline-reference-array-constructors` | Compare initialized object slots, value copies, and closed aliases across CLR, Node, and Edge |
| [175 · Copy to sliced span fields](stage-175-character-span-field-copy/README.md) | `npm run demo -- stage-175-character-span-field-copy` | Copy to a writable field slice with overlap and typed failure parity |
| [176 · String span ranges](stage-176-string-span-ranges/README.md) | `npm run demo -- stage-176-string-span-ranges` | Compare UTF-16 string ranges, null and bounds checks, and argument order across CLR, Node, and Edge |
| [177 · String copy to span](stage-177-string-copy-to-span/README.md) | `npm run demo -- stage-177-string-copy-to-span` | Run original CoreLib string copy into managed and native spans with typed failure parity |
| [179 · Character span search](stage-179-character-span-search/README.md) | `npm run demo -- stage-179-character-span-search` | Compare bounded UTF-16 searches on managed and native-backed views across CLR, Node, and Edge |
| [180 · CoreLib monitors](stage-180-corelib-monitors/README.md) | `npm run demo -- stage-180-corelib-monitors` | Run original AppContext code and verify monitor depth and typed failures |
| [181 · CoreLib event accessors](stage-181-corelib-event-accessors/README.md) | `npm run demo -- stage-181-corelib-event-accessors` | `AppDomain.ProcessExit` subscriptions through CoreLib's original add/remove loops (Delegate.Combine by MethodDef, Interlocked.CompareExchange), matched with the x86 CLR in Node and Edge |
| [182 · Boolean field projections](stage-182-boolean-field-projections/README.md) | `npm run demo -- stage-182-boolean-field-projections` | Compare Boolean aliases to replaced struct-array fields, nested values and ordinary locals in CLR, Node, and Edge |
| [183 · Computed span slice arguments](stage-183-computed-span-slices/README.md) | `npm run demo -- stage-183-computed-span-slices` | Verify computed slice offsets, lengths, aliases and typed failures |
| [184 · Window class registration and procedure replacement](stage-184-window-class-registration/README.md) | `npm run demo -- stage-184-window-class-registration` | Compare Win32 class atoms, module-local attributes, and procedure replacement in the browser host |
| [185 · Character span equality](stage-185-character-span-equality/README.md) | `npm run demo -- stage-185-character-span-equality` | Match the CLR on backing identity, range and empty anchors |
| [186 · Readonly character references](stage-186-readonly-character-references/README.md) | `npm run demo -- stage-186-readonly-character-references` | Compare owner and offset retention and diagnose unverified readonly use across CLR, Node, and Edge |
| [187 · Compiled window class bindings](stage-187-window-class-bindings/README.md) | `npm run demo -- stage-187-window-class-bindings` | Compile SDK-shaped class and window procedure imports for the browser host |
| [188 · Readonly character reference identity](stage-188-readonly-character-reference-identity/README.md) | `npm run demo -- stage-188-readonly-character-reference-identity` | Preserve readonly aliases through exact same-character reinterpretations |
| [189 · Synchronous Win32 window creation](stage-189-window-creation/README.md) | `npm run demo -- stage-189-window-creation` | Compare hidden window creation, current procedure dispatch, and nested callback memory against Windows |
| [190 · Compiled window creation binding](stage-190-window-creation-binding/README.md) | `npm run demo -- stage-190-window-creation-binding` | Compile the SDK-shaped window creation import and dispatch through the production host |
| [193 · Single-character local span](stage-193-single-character-local-span/README.md) | `npm run demo -- stage-193-single-character-local-span` | Preserve local aliasing, distinct frame owners, and empty span anchors |
| [194 · Win32 full-path resolution](stage-194-win32-full-path/README.md) | `npm run demo -- stage-194-win32-full-path` | Compare virtual Windows path resolution, UTF-16 buffer retries and file-part pointers |
| [195 · Compiled Windows full-path binding](stage-195-full-path-native-binding/README.md) | `npm run demo -- stage-195-full-path-native-binding` | Compile the raw UTF-16 full-path import and check argument order, shared memory, and return values |
| [196 · Managed full-path wrapper](stage-196-full-path-managed-wrapper/README.md) | `npm run demo -- stage-196-full-path-managed-wrapper` | Compare scoped UTF-16 buffers and saved Win32 errors against the x86 CLR |
| [197 · Character span IndexOf](stage-197-character-span-index-of/README.md) | `npm run demo -- stage-197-character-span-index-of` | Match the CLR on first-occurrence search over readonly UTF-16 views |
| [204 · General delegate signatures](stage-204-general-delegates/README.md) | `npm run demo -- stage-204-general-delegates` | `Func`/`Action` instantiations and library-declared delegates with any storable signature, matched with the x86 CLR in Node and Edge; delegate casts and multicast diagnosed |
| [220 · Delegates from referenced assemblies](stage-220-referenced-delegates/README.md) | `npm run demo -- stage-220-referenced-delegates` | Delegate TypeDefs from a dependency (TypeRef), across the assembly boundary in both directions and over value-type wrappers, matched with the x86 CLR in Node and Edge |
| [221 · Multicast delegates](stage-221-multicast-delegates/README.md) | `npm run demo -- stage-221-multicast-delegates` | `Delegate.Combine`/`Remove`, field-like events, `Control.Events`-style `Delegate` slots and nominal delegate casts, matched with the x86 CLR in Node and Edge |
| [198 · Virtual executable working directory](stage-198-virtual-executable-directory/README.md) | `npm run demo -- stage-198-virtual-executable-directory` | Launch an unchanged console publish through the real file intake and resolve paths within its virtual directory |
| [200 · Runtime type namespaces](stage-200-type-namespace/README.md) | `npm run demo -- stage-200-type-namespace` | Read canonical metadata namespaces from ordinary, nested, generic, and array types |
| [201 · Default window procedure](stage-201-default-window-procedure/README.md) | `npm run demo -- stage-201-default-window-procedure` | Resolve a resident User32 procedure and verify native hidden-popup defaults and captions |
| [202 · Reference default interface dispatch](stage-202-reference-default-interface/README.md) | `npm run demo -- stage-202-reference-default-interface` | Execute actual default interface bodies through nominal reference dispatch |
| [203 · Redraw requests for hidden windows](stage-203-hidden-window-redraw/README.md) | `npm run demo -- stage-203-hidden-window-redraw` | Compare compiled RedrawWindow calls, borrowed rectangles and last-error behavior against Windows |
| [205 · Registered window messages](stage-205-register-window-message/README.md) | `npm run demo -- stage-205-register-window-message` | Compare Win32 registered message identity, UTF-16 input and last-error behavior across runtimes |
| [206 · UInt32 managed byrefs](stage-206-uint32-managed-byrefs/README.md) | `npm run demo -- stage-206-uint32-managed-byrefs` | Match unsigned managed ref and out parameters on the CLR, Node and Edge |
| [207 · Window owner identity](stage-207-window-owner/README.md) | `npm run demo -- stage-207-window-owner` | Compare owning process and thread, optional PID storage, and invalid HWND errors |
| [208 · Int32 native frame locals](stage-208-int32-native-frame-locals/README.md) | `npm run demo -- stage-208-int32-native-frame-locals` | Compare typed local access and native pointer aliases over stable frame cells |
| [209 · GUI thread handle lifecycle](stage-209-thread-handle-lifecycle/README.md) | `npm run demo -- stage-209-thread-handle-lifecycle` | Compare the launch thread identity, duplicate handles, exit status and errors against Windows |
| [210 · Unboxing a closed generic reference](stage-210-generic-reference-unbox/README.md) | `npm run demo -- stage-210-generic-reference-unbox` | Match generic reference casts, identity and typed failures across runtimes |
| [211 · Native aggregate field pointers](stage-211-native-aggregate-field-pointer/README.md) | `npm run demo -- stage-211-native-aggregate-field-pointer` | Keep field pointers attached to stable blittable frame storage |
| [212 · Scoped native frame layouts](stage-212-scoped-native-frame-layout/README.md) | `npm run demo -- stage-212-scoped-native-frame-layout` | Verify stable frame-field pointers for reference-free ref structs |
| [213 · In-place native frame constructors](stage-213-native-frame-constructor/README.md) | `npm run demo -- stage-213-native-frame-constructor` | Check native field initialization and borrowed pointer aliasing across runtimes |
| [214 · Scoped native instance receivers](stage-214-scoped-native-receiver/README.md) | `npm run demo -- stage-214-scoped-native-receiver` | Verify field readers and disposal on the original native frame slot |
| [215 · Closed type comparisons in CoreLib](stage-215-core-type-branches/README.md) | `npm run demo -- stage-215-core-type-branches` | Compare closed generic type-identity branches across runtimes |
| [216 · Comparable managed reference joins](stage-216-managed-reference-joins/README.md) | `npm run demo -- stage-216-managed-reference-joins` | Preserve reference identity across typed evaluation-stack merges |
| [217 · Constrained enum formatting](stage-217-constrained-enum-format/README.md) | `npm run demo -- stage-217-constrained-enum-format` | Compare enum names, invariant decimal fallback and string identity across runtimes |
| [218 · Static read-only Int32 spans](stage-218-static-int32-spans/README.md) | `npm run demo -- stage-218-static-int32-spans` | Read aligned PE static data through the standard managed span ABI |
| [219 · Exact TypeHandle round-trips](stage-219-exact-type-handle/README.md) | `npm run demo -- stage-219-exact-type-handle` | Preserve descriptor identity through TypeHandle on proven typeof receivers |
| [222 · Explicit type initialization](stage-222-explicit-type-initialization/README.md) | `npm run demo -- stage-222-explicit-type-initialization` | Verify explicit calls to guarded managed initializers |
| [223 · Win32 process window station](stage-223-window-station/README.md) | `npm run demo -- stage-223-window-station` | Compare the borrowed station handle and user-object flags with x86 Windows |
| [224 · Interface types in delegate signatures](stage-224-interface-delegate-abi/README.md) | `npm run demo -- stage-224-interface-delegate-abi` | Verify nominal interface arguments and results in typed delegate thunks |
| [225 · Guarded string-to-span copies](stage-225-proven-string-span-moves/README.md) | `npm run demo -- stage-225-proven-string-span-moves` | Verify original CoreLib copy guards, return values and alias-visible writes |
| [226 · Delegate reference variance](stage-226-delegate-variance/README.md) | `npm run demo -- stage-226-delegate-variance` | Compare covariant and contravariant calls while preserving closure identity |
| [227 · Defaulting inline span fields](stage-227-inline-span-field-defaults/README.md) | `npm run demo -- stage-227-inline-span-field-defaults` | Verify exact field defaults through a shared managed owner and through earlier copies |
| [228 · Readonly aggregate arguments](stage-228-readonly-aggregate-call/README.md) | `npm run demo -- stage-228-readonly-aggregate-call` | Verify nonescaping readonly field arguments, exception identity and null checks |
| [229 · Current managed thread](stage-229-current-managed-thread/README.md) | `npm run demo -- stage-229-current-managed-thread` | Preserve the original CoreCLR Thread identity, managed fields and scoped QCall write |
| [230 · Browser Mono current thread](stage-230-mono-current-thread/README.md) | `npm run demo -- stage-230-mono-current-thread` | Verify the original browser CurrentThread path, identity and context |
| [231 · Runtime type assignability](stage-231-type-assignability/README.md) | `npm run demo -- stage-231-type-assignability` | Compare runtime Type descriptors against a proven literal receiver |
| [232 · Obsolete static fields](stage-232-obsolete-static-fields/README.md) | `npm run demo -- stage-232-obsolete-static-fields` | Verify deprecation metadata and ordinary static storage |
| [233 · Scoped UTF-16 path buffers](stage-233-scoped-utf16-path-buffers/README.md) | `npm run demo -- stage-233-scoped-utf16-path-buffers` | Compare scoped path input and output, resize queries and Win32 error slots with Windows |
| [234 · Runtime Type instance fields](stage-234-type-instance-fields/README.md) | `npm run demo -- stage-234-type-instance-fields` | Preserve canonical Type identity and ordinary struct value copies through readonly fields |
| [235 · Closed generic protected virtual calls](stage-235-protected-virtual/README.md) | `npm run demo -- stage-235-protected-virtual` | Resolve protected declarations and reuse verified virtual dispatch |
| [236 · Window class procedure replacement](stage-236-class-window-procedure/README.md) | `npm run demo -- stage-236-class-window-procedure` | Compare class procedures, future window dispatch and raw Win32 errors with Windows |
| [237 · Compare-exchange on volatile Int32 fields](stage-237-volatile-int32-compare-exchange/README.md) | `npm run demo -- stage-237-volatile-int32-compare-exchange` | Preserve signed Int32 compare-exchange, null behavior and location aliasing |
| [238 · Entry assembly identity](stage-238-entry-assembly-descriptors/README.md) | `npm run demo -- stage-238-entry-assembly-descriptors` | Compare executable identity and overrides through Mono CoreLib |
| [239 · Checked native integer construction](stage-239-checked-native-integer-construction/README.md) | `npm run demo -- stage-239-checked-native-integer-construction` | Compare signed and unsigned native constructors, pointer overloads and typed overflow with Windows |
| [240 · Proven closed interface casts](stage-240-proven-interface-casts/README.md) | `npm run demo -- stage-240-proven-interface-casts` | Preserve exact closed interface identity and null through proven casts |
| [241 · Closed generic static reference addresses](stage-241-static-reference-address/README.md) | `npm run demo -- stage-241-static-reference-address` | Preserve per-instantiation storage across direct and byref accesses |
| [242 · Closed Type comparisons through a local](stage-242-closed-type-local/README.md) | `npm run demo -- stage-242-closed-type-local` | Fold proven Type branches and preserve surviving local values |
| [243 · Generic SZ-array enumeration](stage-243-sz-array-enumeration/README.md) | `npm run demo -- stage-243-sz-array-enumeration` | Run the original closed CoreLib enumerator from a vector interface call |
| [244 · CoreLib Math internal calls](stage-244-corelib-math/README.md) | `npm run demo -- stage-244-corelib-math` | Compare original runtime Math method definitions against the x86 CLR |
| [245 · Strong GC handles](stage-245-strong-gc-handles/README.md) | `npm run demo -- stage-245-strong-gc-handles` | Compare normal handle lifecycle, typed errors and finalizer reachability with Windows |
| [246 · Assembly metadata attributes](stage-246-assembly-metadata-attributes/README.md) | `npm run demo -- stage-246-assembly-metadata-attributes` | Materialize genuine AssemblyMetadataAttribute rows through the original enumerator |
| [247 · Projected native-word exchange](stage-247-projected-native-word-exchange/README.md) | `npm run demo -- stage-247-projected-native-word-exchange` | Compare live native-word aliases and native Interlocked.Exchange with Windows |
| [248 · Class fields with 32-bit enum addresses](stage-248-int32-enum-class-field-projection/README.md) | `npm run demo -- stage-248-int32-enum-class-field-projection` | Compare writable enum field aliases and out parameters with Windows |
| [249 · Display device capabilities](stage-249-display-device-capabilities/README.md) | `npm run demo -- stage-249-display-device-capabilities` | Compare display capability queries and common device-context lifetime with Windows |
| [250 · Direct Int32 receivers](stage-250-direct-int32-receiver/README.md) | `npm run demo -- stage-250-direct-int32-receiver` | Execute original Int32 instance bodies on tracked managed addresses |
| [251 · CoreLib string interface tests](stage-251-string-interface-type-tests/README.md) | `npm run demo -- stage-251-string-interface-type-tests` | Compare dynamic string interface membership, reference identity and typed cast failure with Windows |
| [252 · SZ-array arguments to generic lists](stage-252-sz-array-list-arguments/README.md) | `npm run demo -- stage-252-sz-array-list-arguments` | Compare exact array interface arguments and preservation of original array identity |
| [253 · Constraints on another type parameter](stage-253-dependent-type-constraints/README.md) | `npm run demo -- stage-253-dependent-type-constraints` | Check substituted bounds for closed generic methods |
| [254 · Virtual long-path lookup](stage-254-virtual-long-paths/README.md) | `npm run demo -- stage-254-virtual-long-paths` | Match Windows UTF-16 path expansion over an explicitly mounted browser filesystem |
| [255 · Generic method constructor constraints](stage-255-method-default-constructors/README.md) | `npm run demo -- stage-255-method-default-constructors` | Validate real closed type constructors before specializing methods |
| [256 · Nominal enum boxes](stage-256-nominal-enum-boxes/README.md) | `npm run demo -- stage-256-nominal-enum-boxes` | Preserve runtime identity and checked unboxing for original enum values |
| [257 · 64-bit managed references](stage-257-wide-managed-byrefs/README.md) | `npm run demo -- stage-257-wide-managed-byrefs` | Compare aliased 64-bit locals and instance fields with the CLR |
| [258 · Constrained Int32 object equality](stage-258-constrained-int32-equals/README.md) | `npm run demo -- stage-258-constrained-int32-equals` | Compare the original override, exact box identity and null managed locations with Windows |
| [259 · Zero initialization through managed Int32 locations](stage-259-int32-managed-zero/README.md) | `npm run demo -- stage-259-int32-managed-zero` | Compare zero writes through closed generic local and field aliases with Windows |
| [260 · Identical interface constraints](stage-260-identical-interface-constraints/README.md) | `npm run demo -- stage-260-identical-interface-constraints` | Accept an interface as its own exact nominal bound |
| [261 · Hidden window invalidation](stage-261-hidden-window-invalidation/README.md) | `npm run demo -- stage-261-hidden-window-invalidation` | Match Windows client invalidation and paint timing for hidden top-level HWNDs |
| [263 · Canonical String generic constraints](stage-263-canonical-string-constraints/README.md) | `npm run demo -- stage-263-canonical-string-constraints` | Compare real String interface constraints and closed type identity with Windows |
| [265 · Mono Interlocked increment](stage-265-mono-interlocked-increment/README.md) | `npm run demo -- stage-265-mono-interlocked-increment` | Preserve alias-visible Int32 updates and original TaskScheduler IDs |
| [266 · Runtime Type vectors](stage-266-runtime-type-vectors/README.md) | `npm run demo -- stage-266-runtime-type-vectors` | Compare descriptor cells and authentic Type[] identity with Windows |
| [267 · Runtime generic interface Type descriptors](stage-267-runtime-generic-interface-types/README.md) | `npm run demo -- stage-267-runtime-generic-interface-types` | Resolve linked closed interface types from canonical runtime descriptors |
| [268 · Fieldless explicit-size values](stage-268-fieldless-explicit-size/README.md) | `npm run demo -- stage-268-fieldless-explicit-size` | Preserve managed padding, nominal identity and exact declared size |
| [269 · Boolean class-field byrefs](stage-269-boolean-class-byrefs/README.md) | `npm run demo -- stage-269-boolean-class-byrefs` | Alias writable packed Boolean fields while retaining their owners |
| [270 · Assignability to factory-created interfaces](stage-270-runtime-interface-assignability/README.md) | `npm run demo -- stage-270-runtime-interface-assignability` | Compare canonical interface relations and diagnose incomplete graphs |
| [272 · Boolean storage aliases](stage-272-boolean-storage-aliases/README.md) | `npm run demo -- stage-272-boolean-storage-aliases` | Preserve live aliases through one-field views in the original volatile write |
| [273 · Runtime generic type classification](stage-273-runtime-type-generic-kind/README.md) | `npm run demo -- stage-273-runtime-type-generic-kind` | Verify generic metadata, arrays, null and nominal virtual overrides |
| [274 · Runtime assembly equality](stage-274-runtime-assembly-equality/README.md) | `npm run demo -- stage-274-runtime-assembly-equality` | Compare canonical runtime descriptors by reference identity |
| [275 · Runtime assembly arrays](stage-275-runtime-assembly-arrays/README.md) | `npm run demo -- stage-275-runtime-assembly-arrays` | Resolve authentic assembly array signatures to the same element identity |
| [276 · Runtime generic type definitions](stage-276-runtime-generic-definitions/README.md) | `npm run demo -- stage-276-runtime-generic-definitions` | Verify exact definition identity, fresh typed failures and nominal dispatch |
| [277 · Runtime generic type arguments](stage-277-runtime-generic-arguments/README.md) | `npm run demo -- stage-277-runtime-generic-arguments` | Verify ordered canonical generic arguments and independent reflection arrays |
| [278 · Writable byte spans](stage-278-writable-byte-spans/README.md) | `npm run demo -- stage-278-writable-byte-spans` | Compare ranged byte views and original array aliasing with Windows |
| [280 · Enum type classification](stage-280-runtime-type-enum-kind/README.md) | `npm run demo -- stage-280-runtime-type-enum-kind` | Verify exact Enum ancestry and nominal virtual overrides |
| [281 · Closed reference boxes at interface calls](stage-281-reference-box-provenance/README.md) | `npm run demo -- stage-281-reference-box-provenance` | Preserve reference identity, null, and verified interface call arguments |
| [282 · Readonly static value locations](stage-282-readonly-static-value-location/README.md) | `npm run demo -- stage-282-readonly-static-value-location` | Preserve initonly aggregate addresses and value snapshots across assemblies |
| [283 · CoreLib ordinal string comparison](stage-283-string-ordinal-sequence/README.md) | `npm run demo -- stage-283-string-ordinal-sequence` | Compare original linked string inequality, UTF-16 data and ordinary null and length checks |
| [284 · Exact readonly SZ-array interface assignment](stage-284-readonly-szarray-assignment/README.md) | `npm run demo -- stage-284-readonly-szarray-assignment` | Pass SZ arrays to their exact readonly list and collection interfaces |
| [286 · Copy exact managed value vectors](stage-286-value-vector-copy/README.md) | `npm run demo -- stage-286-value-vector-copy` | Copy immutable snapshots while preserving destination aliases and overlap behavior |
| [287 · Runtime value type classification](stage-287-runtime-value-type-kind/README.md) | `npm run demo -- stage-287-runtime-value-type-kind` | Compare canonical CLR value type identity and protected Type overrides |
| [288 · Constrained Int32 comparison](stage-288-constrained-int32-compare/README.md) | `npm run demo -- stage-288-constrained-int32-compare` | Bind the original Int32 comparison through its exact interface slot |
| [289 · Closed type guards and Boolean branches](stage-289-closed-type-boolean-guards/README.md) | `npm run demo -- stage-289-closed-type-boolean-guards` | Eliminate impossible generic branches and preserve operand evaluation |
| [290 · Direct private calls with a known false argument](stage-290-private-bool-specialization/README.md) | `npm run demo -- stage-290-private-bool-specialization` | Reuse a private Boolean method body with proven normal-flow reachability |
| [291 · Managed native-word class fields](stage-291-native-word-class-fields/README.md) | `npm run demo -- stage-291-native-word-class-fields` | Verify traced mutable native-word fields and typed null handling |
| [292 · Nested value fields in constructors](stage-292-ctor-nested-value-fields/README.md) | `npm run demo -- stage-292-ctor-nested-value-fields` | Preserve nested overlay bytes and outer value snapshots |
| [293 · Scoped pinned value references](stage-293-scoped-pinned-values/README.md) | `npm run demo -- stage-293-scoped-pinned-values` | Compare verified synchronous pointer calls and managed copy-back |
| [294 · Assembly type definitions by name](stage-294-assembly-type-lookup/README.md) | `npm run demo -- stage-294-assembly-type-lookup` | Find canonical CLR TypeDefs by exact name within a chosen assembly |
| [295 · DPI-aware window rectangle adjustment](stage-295-dpi-window-rect/README.md) | `npm run demo -- stage-295-dpi-window-rect` | Compare the original Win32 RECT adjustment with the browser host |
| [296 � Win32 window handle lifetime](stage-296-is-window/README.md) | `npm run demo -- stage-296-is-window` | Compare live, null, invalid and destroyed HWNDs against Windows |
| [297 · Canonical Mono assembly identity boundary](stage-297-mono-assembly-identity/README.md) | `npm run demo -- stage-297-mono-assembly-identity` | Verify original GetName virtual dispatch and the native assembly identity boundary |
| [300 · Native CLR fixed-buffer layout](stage-300-native-fixed-buffer-layout/README.md) | `npm run demo -- stage-300-native-fixed-buffer-layout` | Measure authentic native fixed buffers with exact element and width proofs |
| [302 · Native UTF-8 decoding](stage-302-native-utf8-decoding/README.md) | `npm run demo -- stage-302-native-utf8-decoding` | Match CLR UTF-8 code units, replacement and first-NUL handling |
| [304 · TextInfo ASCII case normalization](stage-304-textinfo-ascii-case/README.md) | `npm run demo -- stage-304-textinfo-ascii-case` | Compare the original Mono helper's string identity and UTF-16 results |
| [306 · Native byte span transfer](stage-306-native-byte-transfer/README.md) | `npm run demo -- stage-306-native-byte-transfer` | Verify bounded bytes and managed aliasing against the CLR |
| [308 · Proven closed interface casts](stage-308-proven-closed-interface-cast/README.md) | `npm run demo -- stage-308-proven-closed-interface-cast` | Preserve CLR references when exact class membership proves the cast |
| [311 · Readonly character location forwarding](stage-311-readonly-character-forwarding/README.md) | `npm run demo -- stage-311-readonly-character-forwarding` | Preserve readonly aliases and bounded offsets through linked managed calls |
| [313 · Readonly signed Int16 span views](stage-313-readonly-int16-span-cast/README.md) | `npm run demo -- stage-313-readonly-int16-span-cast` | Preserve aliased packed storage and signed CLR element reads |
| [314 · Addressable character locals](stage-314-addressable-character-locals/README.md) | `npm run demo -- stage-314-addressable-character-locals` | Preserve UTF-16 out values, aliases and frame lifetime |
| [317 · Readonly ordinal string borrow](stage-317-readonly-ordinal-string-borrow/README.md) | `npm run demo -- stage-317-readonly-ordinal-string-borrow` | Lend the original ordinal caller a readonly packed-string location |
| [320 · Closed Vector128 lane counts](stage-320-vector128-count/README.md) | `npm run demo -- stage-320-vector128-count` | Compare exact platform lane counts and preserve assembly identity |

## Adding a stage

Choose a stable stage number in the tracking issue. Add a stage-NN-name directory containing its project, C# sources, demo.json and README.md. Give each assembly a unique name because it identifies both the artifact and the test module. Copy an existing manifest and specify the assembly, export type, method, inputs and expected value. Library stages build for netstandard2.0 unless the manifest sets `targetFramework` (for example `net10.0`). Document the capability, command and expected result. Add the project reference to Verify/Verify.csproj and CLR comparison cases in Verify/Program.cs, then run the stage command and npm test. Update this index in the same PR. Keep generated files under ignored bin, obj and artifacts directories.

Browser application stages can declare a runner in demo.json. Give them a documented verification command for the native and browser hosts.
