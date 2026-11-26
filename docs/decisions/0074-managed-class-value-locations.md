# 0074 · Managed class value-field locations

A managed class field containing a CLI value type opens as a stable reference
when its address is taken. Calls on that reference can publish field writes
before returning or throwing. Other IL that reads the containing class field
must still receive an independent value copy.

Represent the field with a nullable, private Wasm GC cell. Its contents are an
immutable aggregate snapshot. An untouched field can omit the cell and reads
as a default value. The first write or address operation materializes the cell.
Later writes update its snapshot in place, preserving the identity of any
captured address. The existing immutable snapshots remain valid as value copies.

The verifier retains the CLI type of each location. Direct value-type instance
calls on class fields specialize the same managed method for a cell receiver.
Receiver field reads reload the current snapshot. Receiver field writes publish
the replacement snapshot immediately. The null check happens at `ldflda`,
before argument evaluation. The snapshot-receiver ABI remains available for
previously supported calls.

Stage 146 covers direct calls whose receiver is a class field. General `T&`
parameters, constrained dispatch, nested value locations, address-backed
constructors, and locations in arrays or frame slots are separate work. An
unsupported address use produces a located diagnostic.

The stage compares the same DLL with the x86 CLR, Node and Edge. Its linked
`BitVector32` test verifies the section layout against the official CLR factory
and then executes the original section setter and getter.
