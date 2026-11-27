# 0087 · Aggregate boxes

## Context

Upstream WinForms caches rectangle bounds through `IDictionary` as `object`
and later retrieves them with `unbox.any`. Boxed values can also flow through
interface tests and retain references in their fields. CLR object identity
belongs to each allocation. The stored value is a copy of the input.

## Decision

Boxing a supported non-nullable aggregate allocates a compiler-owned object
with the exact closed type descriptor and an independent value-location cell.
An ordinary value continues to use its immutable snapshot representation.
The cell stores that snapshot and supplies a stable address when the CLI
executes `unbox`. `unbox.any` validates the same descriptor and reads a value
snapshot from the cell. Missing objects and incompatible types raise the
corresponding managed exception.

Concrete type tests, implemented interface predicates and `Object.GetType`
use the same exact type descriptor. The shadow tracer follows the payload
cell and its snapshot, and a retained interior address keeps its box alive.
This design covers ordinary aggregate boxes. Nullable and enum boxing and
invocation of virtual members on boxed aggregates require separate lowering.

## Verification

Stage 161 compares distinct box allocations, identity, field access through
`unbox`, copy isolation, interface tests, and typed failures against the same
.NET Standard DLL on the Windows x86 CLR, Node, and Edge. The upstream layout
cache is exercised in the unchanged WinForms bundle under
[issue #232](https://github.com/SamG-Coder/StandardWasm/issues/232).
