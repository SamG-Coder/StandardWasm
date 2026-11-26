# 0042 · Construct value types through their receiver value

Proposed in [#240](https://github.com/SamG-Coder/StandardWasm/issues/240).

## Context

A CLI instance constructor on a value type has the signature
`void .ctor(S& this, ...)`. `newobj` creates a fresh zeroed `S`, runs the
constructor on that instance's managed address, and pushes the value.
`call S::.ctor` on `ldloca` or on a constructor's own `ldarg.0` runs it on an
existing location without zeroing that location first. Roslyn uses the
in-place form for most local initialization, including reassignment. It uses
`newobj` for arguments, fields and assignments inside protected regions.

[Decision 0033](0033-embedded-sequential-values.md) represents a supported
sequential value as an immutable Wasm GC aggregate, with null as the all-zero
default. A write through a nonescaping local address publishes a replacement
aggregate to that local. There are no general managed byrefs.

## Decision

A value-type instance constructor is compiled to the Wasm function type
`(S this, arguments...) -> S`:

- **Entry.** The receiver parameter holds the location's aggregate when the
  constructor starts. `newobj` passes `ref.null S`, the zero default. An
  in-place `call` passes the location's current value, so a constructor
  that reads before writing sees the CLR's contents.
- **Body.** `ldarg.0` is the receiver's address. It may be followed only by
  `ldfld` (read the current value), `initobj S` (clear), a straight-line
  operand sequence ending in `stfld` (replace the parameter's aggregate), or
  one ending in a `call` to a constructor of the same value type (chained
  `this(...)`). `starg.0` and every other use stay diagnosed, so the address
  cannot escape.
- **Exit.** The IL `ret` has an empty stack and returns the receiver's final
  aggregate.
- **Call sites.** `newobj` pushes the returned value. An in-place call
  (`ConstructValue`) stores the returned value to the addressed local or to
  the enclosing constructor's receiver. Copies taken before the call keep
  their values, because aggregates are immutable.

Only constructors take a value receiver here. [Decision 0049](0049-read-only-value-methods.md)
later admits read-only instance methods. Mutating instance methods, `ref`
arguments, closed generic value constructors, and `callvirt` on a
constructor remain unsupported diagnostics at the method and IL offset.

## CLR difference and its guard

The CLR writes fields through the address as the constructor runs. Here the
location changes only when the constructor returns. The two differ only when
the constructor throws after a partial write and a handler or `finally` in
the same frame then reads the location. An in-place construction inside a
protected range is therefore diagnosed. `newobj` is exact, because its fresh
instance is unreachable when the constructor throws. A constructor that
throws outside every protected range unwinds the frame that owns the
location.

The operand scan between an address and its consuming `stfld` or `call`
admits loads, constants, arithmetic, conversions and field reads. Field reads
observe the location as the CLR would, because no instruction in the sequence
can write it. Calls inside the operand sequence stay diagnosed until the scan
can account for their stack effect.

See [Stage 111](../../demos/stage-111-value-construction/README.md).
