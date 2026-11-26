# Stage 89 · Unmanaged null pointer fields

Run `npm run demo -- stage-89-unmanaged-pointer-fields` and `npm test`.
Two compiled .NET Standard assemblies pass null `void*` values through
linked fields and managed calls. The field starts null and remains null
through stores, loads and a branch that selects a pointer.

Since [Stage 137](../stage-137-stackalloc/README.md), a `void*` is a native i32
address into memory 0 (decision 0060), like the primitive pointers of Stage 132.
Any address can be stored, a read through the null field raises
`NullReferenceException`, and a returned null exports as 0. Until then the field
used the nullable bottom reference type and could hold only null. Pointer
signatures remain distinct from native integers, managed byrefs and function
pointers when binding. Function pointer fields stay diagnosed.

See the [unmanaged pointer field decision](../../docs/decisions/0032-unmanaged-void-pointer-fields.md).
