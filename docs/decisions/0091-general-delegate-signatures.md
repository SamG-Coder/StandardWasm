# 0091 · General delegate signatures

Status is proposed in #256 (Stage 204). It builds on decision 0088's single delegate
representation and decision 0013's delegate exceptions.

## Problem

Only three delegate shapes compiled: ``Func`1<int32>`` (the scalar `Delegate` closure),
`EventHandler` and the `SpanAction<char, IntPtr>` callback. Each had a fixed thunk function type, and
`WasmBinary` told the thunks apart by the target's arity. `Func<int, int>`, `Action<string>`, two-
parameter delegates and every delegate type a library declares were read as closed generic classes
or ordinary classes. Their runtime-provided `.ctor` and `Invoke` have no IL, so they failed as -2.

## Decision

- **A registry of delegate shapes.** `DelegateShapes` keeps one entry per closed delegate type the
  program reaches. Each entry has a storage key in `[0x300, 0x400)`, below the class keys, so no code
  takes a delegate for a class layout. An entry records its kind, identity, Invoke parameter storages
  and result storage. Entries live in the layout registry's auxiliary area, linked from
  `ObjectLayouts.DelegateHead`.
  - **Platform delegates.** ``System.Func`1..`5`` and ``System.Action`1..`4`` instantiations, keyed
    by kind and **nominal** type arguments. The same instantiation from any assembly is one entry.
    The Wasm storage of each argument gives the Invoke shape and is stored beside the identity.
    `Action<int>` and `Action<uint>` share a shape but are distinct keys, so a closed generic over
    them (`Slot<Action<int>>`) keeps its own type identity and static fields (Codex review of
    f6e0ba1). Only exactly ``Func`1<System.Int32>`` keeps decision 0088's `Delegate` storage;
    `Func<uint>` is general.
  - **Delegate TypeDefs.** A non-generic class extending the platform `System.MulticastDelegate`,
    keyed by assembly and row. Its Invoke MethodDef gives the signature. `BundleLayouts.RegisterType`
    asks `GeneralDelegates.DefinedStorage` before registering a class layout.
  - **Storable signatures only.** Parameters and results may be `int32`, narrow integers, `int64`,
    `float32`, `float64`, native integers, `string`, `object`, reference classes and other delegate
    keys. Anything else (value types, interfaces, byrefs, generic parameters) keeps the old reading
    and its diagnostics.
- **One closure, a thunk type per shape.** Every key's value is the heap-3 closure `(eqref target,
  funcref method)`, the same struct as decision 0088's delegates. The module appends one function
  type per entry, `(eqref target, p1..pn) -> r`, after every fixed type, at
  `descriptorHeap + descriptors + primitive identities`. The thunk table's second half records each
  thunk's key, negative for an open static target. That replaces the arity rule for these thunks,
  and their type index may need a multi-byte LEB.
- **Construction.** `ldftn target; newobj D::.ctor(object, native int)` lowers to `NewDelegate`
  with the key. That covers a MemberRef `.ctor` on a platform instantiation and a MethodDef `.ctor`
  with Runtime implementation flags on a delegate TypeDef. Linking checks that the target matches the
  Invoke signature exactly: receiver first for an instance target, none for an open static target.
- **Invocation.** `callvirt D::Invoke` becomes a call to the key's invoke helper, a synthetic function
  `(closure, p1..pn) -> r`:

  ```
  local.get 0; struct.get 3 0        ;; target
  local.get 1 .. n                   ;; arguments
  local.get 0; struct.get 3 1        ;; method
  ref.cast (ref thunk-type)          ;; checked by the engine; always succeeds by construction
  call_ref thunk-type
  ```

  The callvirt null check runs before the helper, so a null delegate raises the typed
  `NullReferenceException` (decision 0013).

## Semantics

Stage 221 (decision 0092) replaces this decision's heap-3 storage for keys with nominal
invocation lists. That lifts the type-identity and multicast limits below.


- **Type identity.** The shared closure carries no delegate type, so a run-time cast or type test
  can't tell `Combine` from `Sink`. `castclass`, `isinst` and `is` on these delegates stay -2, even
  from `object` back to the same type, and so does `Delegate.Combine` (`+=`) on them. `EventHandler`
  composition is unchanged.
- **Self-referential signatures.** A delegate whose Invoke takes or returns its own type re-enters
  registration. The layout depth bound turns that into -2.
- **Referenced assemblies (Stage 220).** A MemberRef `.ctor` or `Invoke` whose parent TypeRef
  resolves to a delegate TypeDef in another bundle assembly takes that TypeDef's key. Keys are
  bundle-wide, so they belong to the shared cross-assembly ABI (`IsSharedBundleType`). Value-type
  parameters and results are storable and pass as in an ordinary call, and thunk types use
  `ObjectLayouts.Representation` like every function signature. Interface types are separate work.
- **MemberRef `ldftn`.** An `ldftn` whose token is a MemberRef (a method in another bundle
  assembly, or a lambda in a closed generic class) resolves through `AssemblyBundle.Resolve` like a
  non-virtual call. It then shares the MethodDef path's delegate-target checks
  (`LinkFunctionPointer`). A virtual target stays -2.
- **Not yet:** generic delegate TypeDefs, `ldvirtftn` targets, closed static targets, delegate
  equality, `Target`/`Method`, `BeginInvoke`/`EndInvoke` and interface-typed parameters. All are
  diagnosed.

## Verification

`npm run demo -- stage-204-general-delegates` runs the fixture on the x86 CLR 10.0.10 and compares
Node and Edge with it. The fixture has 20 value kernels and 5 `Reject*` kernels (24 and 1 after Stage 221) that must be
diagnosed.
