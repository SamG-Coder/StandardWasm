# 0073 · Whole structs through native addresses

Status is proposed in #275. It builds on decision 0071 (struct pointers and
`sizeof`). It handles native addresses only, per the boundary agreed with Codex
(handoff 040/095). Managed locations stay traced byrefs (#271, #273).

A managed struct value is an immutable Wasm GC struct, and a null reference is
its default value. These instructions move a blittable sequential struct `S`
between that aggregate and memory 0:

- **`ldobj S`** (C# `Rect r = *p`).
  - Tests the address for 0 (NullReferenceException).
  - Loads every stored layout ordinal at its decision-0071 offset, building
    nested struct fields first.
  - Ends with `struct.new S`.
- **`stobj S`** (C# `*p = r`, `p[i] = r`).
  - Both operands are evaluated, then the address is tested.
  - A null (default) aggregate is materialized as zero.
  - Each ordinal is read with `struct.get` and stored at its offset. Nested
    values are materialized and stored recursively.
- **`initobj S`** at a native address (C# `*p = default`).
  - Tests the address, then `memory.fill`s `sizeof(S)` bytes with zero.
  - `initobj` of a managed local is still lowered as before.

## Plans

Linking builds one plan per struct type: for each stored ordinal, its offset,
its `ldind` opcode, or its nested plan. The plan reaches emission the way type
initializer guards do. A nested aggregate uses its own type's spill local at the
instruction's depth. That slot holds the outer struct, so the nested type's
bank there is free.

## Enum fields

An enum-typed field is a scalar of its underlying width, both in memory and in
the managed registry. The layout reports its underlying `ldind` (for example
`ldind.u1` for a `byte` enum) rather than treating it as a nested struct. This
covers whole-struct copies and decision 0071's `p->F` field access, which would
otherwise use a 4-byte load or store (Codex's review of #296).

## Padding

ECMA-335 leaves padding bytes unspecified. The CLR copies them along with the
struct, but the GC aggregate has no padding. `stobj` writes only fields and
leaves padding bytes as they were. The Stage 145 memory images therefore use
padding-free layouts. A program can't rely on padding contents.

## Still diagnosed

- explicit-layout and auto-layout structs (decision 0071)
- `ldobj` or `stobj` through a managed byref (#271, #273)
- writes to a nested field of a managed local through `ldloca`, a managed-address
  pattern (#271)
- `cpobj` and `cpblk`
