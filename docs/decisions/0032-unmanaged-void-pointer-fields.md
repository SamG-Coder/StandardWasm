# 0032 · Preserve null unmanaged void pointers

Proposed in [#197](https://github.com/SamG-Coder/StandardWasm/issues/197).

Keep the CLI `void*` signature and stack category distinct from native
integers, managed byrefs and function pointers. The current Wasm GC layout
uses the nullable bottom reference type for admitted `void*` fields and
managed method slots. Its only inhabitant is null, including in a
default-initialized field.

The compiler proves a native zero when the literal immediately feeds a
native conversion with no alternate incoming branch. A proven zero can
enter a pointer slot. The compiler preserves this distinction through
evaluation-stack joins and emits explicit null at the destination.
Ordinary native integers cannot enter a pointer slot.

Dereferencing, non-null pointer producers, pointer exports, typed pointers,
and invoking native entry points need their own memory and interop semantics.
This decision does not set the representation for GC interior references.

See [Stage 89](../../demos/stage-89-unmanaged-pointer-fields/README.md) and
[ECMA-335, Partition III](https://www.ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf).
