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
| [27 · Portable compiler diagnostics](stage-27-portable-diagnostics/README.md) | `npm run demo -- stage-27-portable-diagnostics` | BoxedEnum yields -2 for unsupported nominal enum boxing |
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
| [198 · Virtual executable working directory](stage-198-virtual-executable-directory/README.md) | `npm run demo -- stage-198-virtual-executable-directory` | Launch an unchanged console publish through the real file intake and resolve paths within its virtual directory |
| [201 · Default window procedure](stage-201-default-window-procedure/README.md) | `npm run demo -- stage-201-default-window-procedure` | Resolve a resident User32 procedure and verify native hidden-popup defaults and captions |

## Adding a stage

Choose a stable stage number in the tracking issue. Add a stage-NN-name directory containing its project, C# sources, demo.json and README.md. Give each assembly a unique name because it identifies both the artifact and the test module. Copy an existing manifest and specify the assembly, export type, method, inputs and expected value. Library stages build for netstandard2.0 unless the manifest sets `targetFramework` (for example `net10.0`). Document the capability, command and expected result. Add the project reference to Verify/Verify.csproj and CLR comparison cases in Verify/Program.cs, then run the stage command and npm test. Update this index in the same PR. Keep generated files under ignored bin, obj and artifacts directories.

Browser application stages can declare a runner in demo.json. Give them a documented verification command for the native and browser hosts.
