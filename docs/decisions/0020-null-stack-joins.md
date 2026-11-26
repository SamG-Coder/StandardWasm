# 0020 · Preserve CLI null literals across stack joins

Proposed in [#132](https://github.com/SamG-Coder/StandardWasm/issues/132).

Represent `ldnull` as the null verification type until it reaches a typed
stack join, local, field or call. At a join it can refine to the other
incoming reference type. Revisit affected instructions until their stack
states stabilize. Distinct concrete class layouts still require explicit
assignment or conversion.

Load an untyped null from a shared, default-initialized Wasm `nullref` local.
When an edge carries that value into a statically typed stack slot, write
the corresponding typed spill on that edge. A loop can otherwise leave a
previously evaluated instance in the spill on the next null iteration.

See [ECMA-335 verification types](https://ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf)
and [WebAssembly 3.0 reference types](https://webassembly.github.io/spec/core/binary/types.html).
