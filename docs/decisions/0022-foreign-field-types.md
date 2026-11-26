# 0022 · Resolve field types across DLLs

Proposed in [#136](https://github.com/SamG-Coder/StandardWasm/issues/136).

The metadata reader reserves a layout for the owning assembly and
TypeDef before decoding its fields. It returns the storage type or the
image-local TypeDefOrRef for each field. The bundle linker resolves
those references with the same rules as local variables and method
signatures, then fills the reserved slots. Reserving first preserves a
single layout per identity when fields repeat or refer to a class already
being registered.

The Wasm GC emitter uses the existing recursive type group for managed
class references. Fields start as null and retain their declared heap type.
The existing assembly identity and sealed-class restrictions still apply.

See [ECMA-335 field signatures](https://ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf).
