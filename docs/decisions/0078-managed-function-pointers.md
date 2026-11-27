# 0078 · Managed function pointers (`delegate*` and `calli`)

Status is proposed in #275 (Stage 150). Claude claimed it on 2026-11-27, and
Codex agreed (handoff 161). It builds on decision 0060: native ints are i32 in
memory 0.

## Representation

A managed function pointer is a native int, as on the CLR. It can be stored in
locals, fields, arrays and memory 0, and compared. Its value is a slot in
module table 2, a funcref table:

- **`ldftn M`** of a static method that does not feed a delegate constructor is
  lowered to `i32.const k`. `k` is `M`'s slot, assigned at link time. Slot 0
  stays null, so a null pointer cannot call anything.
- **Identity.** One method always gets one slot, so two loads of `&M` compare
  equal, as on the CLR.
- **`ELEMENT_TYPE_FNPTR`.** Locals, parameters and fields of this type are
  stored as `Native`. `SignatureExtents` skips the embedded method signature.

## Calls

`calli <StandAloneSig>` with the default managed calling convention (first byte
`0x00`) pops the arguments and the pointer. It is lowered to
`call_indirect (type T) 2`.

`T` is a function type for the call-site signature. Function types sit in
singleton recursion groups after the class group, so structurally equal types
canonicalize. A callee with the same lowered signature therefore passes the
engine's check.

- **Carrier functions.** `T` is the type of a synthetic carrier function
  (`rows = 0`, body `unreachable`), one per distinct call-site signature.
  Function type indices are `typeOffset + function`, and `typeOffset` grows as
  classes are discovered. Each carrier therefore keeps a `WasmIndexFixups`
  chain, resolved in `WriteModule`.
- **Mismatches.** A mismatched callee signature is an engine trap. The CLR's
  behaviour is undefined, so this is a refinement.
- **Null pointers.** A null pointer is an engine trap too. The CLR raises an
  access violation, which is not a catchable managed exception.

## Tables

| Table | Contents |
| --- | --- |
| 0 | delegate thunks (funcref) |
| 1 | the Stage 149 pin registry (eqref) |
| 2 | function-pointer targets (funcref), slot 0 null |

Each lower table is emitted, possibly empty, whenever a higher table exists, so
the indices stay fixed.

## Still diagnosed

- unmanaged calling conventions (`delegate* unmanaged[...]`, `calli` with a
  non-default convention): P/Invoke and host imports, #274
- `ldvirtftn` into a pointer, and instance-method pointers (`HASTHIS` call sites)
- vararg `calli`
