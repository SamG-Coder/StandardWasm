# 0069 · Read-only object spans over inline arrays

A `ReadOnlySpan<object>` holds a traced reference to its backing vector, a start index, and a length. A null Wasm descriptor represents the CLR's empty default. The descriptor can be copied into managed locals or passed to managed methods. Copies retain their owner and continue to read its current cells.

Keep the original compiler-generated inline-array conversion. Validate the exact platform signatures and helper body before lowering its `Unsafe.AsRef`, `Unsafe.As`, and `MemoryMarshal.CreateReadOnlySpan` operations. The CLR helper gets the verified fixed length of the inline array. A read through the platform indexer checks the signed index against the logical length and raises the same typed CLR exception. The supported indexer form reads the returned readonly reference immediately.

The descriptor stays in compiler-verified managed frames. Raw ref-return uses, spans over other element types, slicing, heap storage and pointer access need their own verification. Character spans and the remaining CoreLib resource machinery are separate work under issue #273.
