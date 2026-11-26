# 0031 · Preserve volatile instance field metadata and access order

Proposed in [#194](https://github.com/SamG-Coder/StandardWasm/issues/194).

Recognize `modreq(System.Runtime.CompilerServices.IsVolatile)` from a known
platform TypeRef or the CoreLib definition. The field still has its underlying
storage type. Link a marked field signature only against a signature with the
same recognized marker and underlying type.

The IL prefix governs each access. Decode the prefix and its direct instance
field operation together. Emit every field operation in program order on the
current single-agent Wasm GC heap. The heap has no shared fields. Static,
indirect, unmanaged and cross-agent operations need separate implementations
with the ordering guarantees appropriate to their memory model.

See [Stage 88](../../demos/stage-88-volatile-field-signatures/README.md) and
[ECMA-335, Partitions I and III](https://www.ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf).
