# 0033 · Preserve copy semantics for embedded values

Proposed in [#200](https://github.com/SamG-Coder/StandardWasm/issues/200).

Bind each supported sequential CLI value type by assembly and TypeDef.
Its Wasm representation is an immutable GC aggregate. The nullable
representation denotes the all-zero default, and field reads materialize
that default when required. Copies may share an immutable representation.
A write through a nonescaping local address creates a replacement aggregate
and changes only that local, so earlier copies keep their values.

Local initialization and field access lower only narrow patterns with
known provenance and no intervening control-flow entry or address escape.
Explicit layout, custom packing, raw interop, value arrays, boxing, and
general managed byrefs need separate implementations.

See [Stage 91](../../demos/stage-91-embedded-value-types/README.md).
