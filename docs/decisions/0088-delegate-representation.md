# 0088 · One delegate representation, cached lambdas and open static targets

Status is proposed in #256 (Stage 164). Codex reserved the stage and decision numbers
(handoff codex-ack-claude-256-stage164). This builds on Stage 119's framework delegates and
decision 0013's delegate exceptions.

## Problem

The compiler had two delegate representations, chosen per signature reader:

- Method and local signatures map ``System.Func`1<int32>`` to the scalar `Delegate` closure
  (`SignatureTypes`, through `DelegateMetadata.ParseFunc`).
- **Field storage** (`BundleLayouts.ReadFieldStorage`) sent the same GENERICINST type to
  `ManagedReferenceSignatures.ReadStorage`, which registers it as a closed generic class.

C# caches every non-capturing lambda in a static field of its `<>c` class:
`ldsfld <>9__N; dup; brtrue; pop; ldsfld <>9; ldftn; newobj Func<int>; dup; stsfld <>9__N`. The
stored value was a `Delegate`, but the field was a class, so the store failed stack analysis with
-1 ("malformed") on valid IL. Main 637030d reproduced this with an ordinary `Run(() => 5)`. The
#232 line reached it once closed generic calls compiled.

Delegates over static methods (`Func<int> f = Five;`, an `EventHandler` subscribed with a static
method) were -2: NewDelegate accepted only instance targets.

## Decision

- **One rule for a delegate type's storage.** `SignatureTypes.GenericDelegateStorage` recognizes a
  GENERICINST delegate signature, which today means ``Func`1<int32>`` → `Delegate`. Method signatures,
  local signatures and instance and static field storage all call it first, so a delegate type has
  the same Wasm storage wherever it's declared. An unrecognized delegate instantiation keeps the
  generic-instance reading and its diagnostics.
- **Open static targets.** `ldnull; ldftn static; newobj D(object, IntPtr)` binds a null target:
  - `Func<int>`: a static target with no parameters returning `int32`.
  - `EventHandler`: `(object, EventArgs) -> void`.

  The thunk has the delegate's ordinary thunk type, `(eqref) -> i32` or `(eqref, eqref, EventArgs)
  -> void`, and ignores its leading target parameter. Both closure structs hold a nullable target,
  so nothing else changes, including invocation, event lists, `Combine` and `Remove`.
- **Arity identifies the thunk.** Link time admits exactly these target shapes. So in
  `WasmBinary`, arity says whether a target is static (0 or 2) or instance (1 or 3), which keeps the
  thunk table a plain list of function indices.

## Semantics

- **Null targets.** An instance delegate over a null target raises `ArgumentException` at
  construction, as measured on CLR 10.0.10 x86, and that behavior is unchanged. (A probe that never
  used the delegate seemed to show no exception, because the JIT removed the dead construction.
  Stage 164's kernel uses the delegate.) An open static target binds null legitimately, as on the CLR.
- **Closed static targets** (a static method closed over its first argument, such as an
  extension-method delegate) stay -2. The CLR binds the target as the first argument. That needs
  its own receiver rule, deferred to the next milestone.
- **Invoking a null delegate** still raises the typed `NullReferenceException` (decision 0013).

## Not yet

General delegate signatures (`Action`, `Func<T, …>`, custom delegate types), multicast
`Delegate.Combine` beyond `EventHandler`, `ldvirtftn` targets and delegate equality are the next
#256 milestone. Each needs a per-signature thunk type and closure in place of the two fixed shapes.

## Verification

`npm run demo -- stage-164-delegate-representation` compares 11 kernels of an ordinary .NET
Standard 2.0 fixture with the x86 CLR in Node and Edge. They cover:

- cached lambdas, reused across calls and inside `try`
- static method groups in locals and in static and instance fields
- a static `EventHandler` subscribed, raised, then removed
- instance and static targets mixed
- a null delegate invoked
- the CLR's `ArgumentException` for an instance delegate over null
