# Stage 89 · Unmanaged null pointer fields

Run `npm run demo -- stage-89-unmanaged-pointer-fields` and `npm test`.
Two compiled .NET Standard assemblies pass null `void*` values through
linked fields and managed calls. The field starts null and remains null
through stores, loads and a branch that selects a pointer.

The Wasm GC field uses the nullable bottom reference type. A proven CLI
native zero can enter a pointer slot, and an ordinary native integer
cannot. Pointer signatures remain distinct from native integers, managed
byrefs and function pointers. The current browser path has no non-null
pointer producer and diagnoses dereference, pointer exports and unsupported
pointer kinds.

See the [unmanaged pointer field decision](../../docs/decisions/0032-unmanaged-void-pointer-fields.md).
