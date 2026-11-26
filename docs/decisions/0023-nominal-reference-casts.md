# 0023 · Preserve class identity during reference casts

Proposed in [#138](https://github.com/SamG-Coder/StandardWasm/issues/138).

Unrelated CLI classes can have identical field shapes. Encode the
managed classes as distinct positions in one Wasm GC recursive group,
so an intra-module type test can distinguish them. Resolve the cast
target through the existing bundled type resolver before emitting
the heap test.

Use a nullable type test and cast. A failed `isinst` returns null. A
failed `castclass` raises the module's `InvalidCastException` tag before
issuing the Wasm cast. A null source stays null in either operation.
The EventHandler conversion already validated by delegate lowering keeps
its existing behavior.

This covers the compiler's supported sealed classes. Managed object
references are scoped to one compiled module.

See [ECMA-335 Partition III](https://ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf)
and [WebAssembly defined types](https://webassembly.github.io/spec/core/valid/conventions.html#defined-types).
