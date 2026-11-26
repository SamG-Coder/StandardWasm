# 0029 · Preserve closed generic reference identity

Proposed in [#189](https://github.com/SamG-Coder/StandardWasm/issues/189).

A closed reference type is identified by the resolved assembly, TypeDef and
ordered type arguments. Each instantiation has its own registered layout.
The field reader substitutes class type parameters from that layout, including
inside vectors such as `T[]`. The linker can follow the definition through a
normal type forwarder before applying the substitution.

Reference vectors use mutable Wasm GC arrays. The linker tracks their nominal
element types even when the Wasm representation of an interface element is
`eqref`. The current compiler accepts exact vector assignments and checks
each store against the declared element type. Null and bounds failures retain
their managed exception tags.

Specialized constructor and method bodies use a method identity that includes
the declaring instantiation ([decision 0037](0037-closed-generic-method-bodies.md)). Generic base classes, nominal constraints,
value-type arguments, and reference array covariance also need additional
implementation. See [Stage 86](../../demos/stage-86-closed-generic-layouts/README.md).
