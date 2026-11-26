# Stage 88 · Volatile instance fields

Run `npm run demo -- stage-88-volatile-field-signatures` and `npm test`.
Two compiled .NET Standard assemblies use volatile integer, packed integer
and reference fields. The generated module preserves stores and reads through
a call, preserves reference identity, and reports a managed null access.

Binding requires the known platform `IsVolatile` marker. A bare field signature
is distinct from a signature with the required marker. The instruction prefix
and metadata declaration have separate roles. Direct managed field accesses
run on one Wasm agent and retain their instruction order. Static field access,
shared-heap concurrency and indirect volatile operations need separate support.

See the [volatile field decision](../../docs/decisions/0031-volatile-instance-fields.md).
