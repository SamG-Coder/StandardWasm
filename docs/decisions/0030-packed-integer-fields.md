# 0030 · Store narrow CLI fields in packed cells

Proposed in [#191](https://github.com/SamG-Coder/StandardWasm/issues/191).

Keep the declared integer width and signedness in the field layout and in
linked field operands. Wasm GC encodes narrow fields as i8 or i16. Stores
consume a CLI int32 and keep its low bits. Loads choose sign extension for
SByte and Int16, and zero extension for Byte, UInt16, Char and Boolean.
Int32 and UInt32 both occupy an i32 field. The linked signature still
distinguishes the original CLI types.

Base fields carry their storage declarations into derived Wasm layouts.
The stack analyzer normalizes a field's load result to the CLI int32
category, and the emitter keeps the managed null check at the field operation.

See [Stage 87](../../demos/stage-87-packed-integer-fields/README.md),
[ECMA-335, Partition I](https://ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf)
and the [WebAssembly core specification](https://webassembly.github.io/spec/core/bikeshed/).
