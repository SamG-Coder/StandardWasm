# 0093 · Virtual delegate targets (`ldvirtftn`)

Status is proposed in #256 (Stage 364). The integration session (Claude, taking over Codex's lanes) agreed the record kind and checked both shared changes below against the #303 slices. It builds on decisions 0091 and 0092.

## Problem

C# compiles a method group over a virtual method, such as `new EventHandler(this.OnClick)` for a `protected virtual OnClick`, to `dup; ldvirtftn M; newobj D::.ctor`. The `ldvirtftn` opcode (0xFE 0x07) wasn't decoded, so every such delegate failed with -2. WinForms raises many of its events through virtual `OnX` methods.

## Decision

- **Decode.** `ldvirtftn` with a MethodDef token becomes `LoadVirtualFunction` (record kind 402; #256 kinds are 400 and above, apart from the integration slices). A MemberRef or MethodSpec token, meaning a method in another assembly or a generic virtual method, stays a located diagnostic.
- **The `dup` shape is required.** The object `ldvirtftn` pops must be the delegate's target, which is what C# emits. Then a thunk that dispatches on its target selects exactly the CLR's method. Any other shape is -2.
- **Binding.**
  - A non-virtual or `final` M binds directly, the same as `ldftn`.
  - Otherwise the thunk binds to **M's existing virtual dispatcher**. `VirtualDispatch.Add(M, declaring class)` returns an ordinary function `(receiver, p1..pn) -> r`, and `VirtualDispatch.Expand` fills its arms with every allocated override, exactly as for `callvirt`.

  So the delegate needs no new site machinery. The thunk calls the dispatcher like any instance target through `LinkFunctionPointer`, and one site serves every target bound through M.
- **Emission.** `LoadVirtualFunction` pops the object, raises the typed `NullReferenceException` when it's null (where the CLR raises it at `ldvirtftn`), and pushes the thunk's table index.
- **Shared changes.**
  - `NewDelegate` accepts a target whose stack type is a subclass of the delegate's receiver. Before this, it required equality and reported -1. It reads the target from that type's own spill. The closure keeps the target as `eqref`.
  - Thunks pass an interface-typed receiver as `eqref` instead of casting it to a struct, matching `ObjectLayouts.Representation` in function signatures.

## Semantics

- Overrides, sealed overrides, non-overridden virtuals, the base instance, abstract methods and interface methods all select the CLR's method.
- EventHandler composition and `==` work on these delegates. Method identity is the thunk, the same for every target bound through M, while the target distinguishes instances. This matches the CLR's `Delegate.Equals`, which compares the resolved method and the target.
- **Not yet:**
  - `ldvirtftn` of a method in another assembly (MemberRef)
  - generic virtual methods (MethodSpec)
  - a bare `ldvirtftn` function pointer that feeds no delegate

## Verification

`npm run demo -- stage-364-virtual-delegate-targets` runs the fixture on the x86 CLR 10.0.10 and compares Node and Edge with it. The fixture has 9 value kernels and 1 `Reject*` kernel, which must be diagnosed. The value kernels include one target bound through three receiver classes in one module, which exercises the shared dispatcher's arms.
