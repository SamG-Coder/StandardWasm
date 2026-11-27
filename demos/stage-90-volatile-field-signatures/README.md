# Stage 90 · Volatile fields

Run `npm run demo -- stage-90-volatile-field-signatures` and `npm test`.
Two compiled .NET Standard assemblies use volatile integer, packed integer
and reference fields. The generated module preserves instance and static
stores and reads through a call, preserves reference identity, and reports a
managed null access.

Binding requires the known platform `IsVolatile` marker. A bare field signature
is distinct from a signature with the required marker. The instruction prefix
and metadata declaration have separate roles. Direct managed field accesses
run on one Wasm agent and retain their instruction order. Shared-heap
concurrency and indirect volatile operations need separate support.

See the [volatile field decision](../../docs/decisions/0031-volatile-instance-fields.md).
