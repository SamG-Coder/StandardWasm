# 0009 · Preserve arithmetic exceptions across rethrow

Status: proposed in [#110](https://github.com/SamG-Coder/StandardWasm/issues/110).
Extends [0008](0008-nested-arithmetic-catches.md).

Use `try_table catch_ref` to keep the actual Wasm exception reference.
Dispatch matches the tag and the throwing IL offset. An unmatched exception
leaves the method through `throw_ref`, preserving its reference across
dispatcher frames.

A method containing CIL `rethrow` has a separate reference local for each
catch. On handler entry the dispatcher saves the captured reference in that
handler's local, then resumes with an empty CIL evaluation stack. Validation
binds `rethrow` to its innermost enclosing catch. The emitted `throw_ref`
loads that catch's reference. A nested catch can keep its own reference
without overwriting the exception of an enclosing catch.

The compiler diagnoses `rethrow` outside catch bodies. Exception
references remain internal to the Wasm backend. Managed exception objects
and explicit CIL `throw` require further lowering.

References: [ECMA-335, III.4.24](https://ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf#page=448)
and [WebAssembly execution semantics for throw_ref](https://webassembly.github.io/spec/core/exec/instructions.html#xref-syntax-instructions-syntax-instr-control-mathsf-throw-ref).
