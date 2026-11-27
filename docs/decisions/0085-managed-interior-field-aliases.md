# 0085 · Managed interior field aliases

## Context

The upstream WinForms startup path reaches `Dictionary<TKey,TValue>.FindValue`,
which returns a managed reference to a field of an entry stored in an array.
One reachable instantiation uses the `System.Value` struct from
`System.Private.Windows.Core`, so the reference can name an aggregate with its
own addressable fields. In CLR storage, a reference to a child field survives
replacement of any ancestor value. A copied aggregate keeps its old value.

## Decision

An address of a supported struct field keeps the parent value cell and a
selector for the bound layout and field. The selector records the closed
metadata identity so that ordinary indirect loads and stores use the existing
CLI byref types at calls and returns. Packed or unsupported terminal locations
continue to produce a compiler diagnostic.

An aggregate projection is a value cell whose payload is an object-location
pair. Ordinary value cells hold an immutable nominal snapshot. The fixed Wasm
recursive group containing the location pair starts with a non-final native
array. The nominal layout group declares final arrays, which keeps these two
groups structurally distinct even when a nominal snapshot has the same fields
as the pair. Stage 158 exercises such a snapshot.

Projection reads follow the parent cell to its current snapshot. Writes rebuild
that snapshot and update the parent cell. For a chain of projections, each
reconstructed value writes back through the next parent until it reaches the
root cell. Untouched fields keep their current contents and previously copied
snapshots remain independent. Modules emit the shared projection access helpers
only for location kinds used by reachable code.

The finalizer shadow tracer follows the first-field edges of the two location
representations to the current snapshot. In modules that emit shadow root
tracing, a slot cell carries an additional immutable edge to its containing
array or object. Replacing the snapshot keeps that edge intact. Frame and
projection cells keep no second owner because their existing payload edge
already leads to the appropriate storage. Stage 152 checks that a finalizable
object in the container survives a live direct or projected alias and becomes
eligible after the borrowing method returns.

## Verification

Stage 158 tests scalar and reference terminals, generic byref returns, mixed
ordinary and projected references, nested aggregate replacement, and a
descriptor-shaped snapshot against the Windows CLR in Node and Edge. Stage 121
also checks whole-receiver replacement through the original `stobj` path.
Track the unchanged WinForms bundle under [issue #232](https://github.com/SamG-Coder/StandardWasm/issues/232).
