# Stage 91 · Embedded sequential value types

Run `npm run demo -- stage-91-embedded-value-types` and `npm test`.
Two .NET Standard libraries exchange sequential values in fields and managed
calls. The suite checks default initialization, copy independence, packed
component reads, and the default of a value that contains a `void*` field.

Values use immutable Wasm GC aggregates. A null representation denotes the
default value. A narrowly scoped update through a local address replaces
the local with a new aggregate, preserving earlier copies. Escaping byrefs,
overlapping native layout, boxing, value type exports, and value arrays need
separate implementation.

See the [embedded value layout decision](../../docs/decisions/0033-embedded-sequential-values.md).
