# 0008 · Nested and disjoint arithmetic catches

Status: proposed in [#108](https://github.com/SamG-Coder/StandardWasm/issues/108).
Extends [0007](0007-arithmetic-catch-order.md).

Preserve the ordered CIL exception table in the portable compiler. Validate
that pairs of entries are disjoint, nested in a single enclosing try or
handler, or siblings sharing one protected range. A try that encloses
another entry must follow that entry in the table. Validate ordinary
branches and leave across all enclosing regions. See [ECMA-335, Partition I,
§12.4.2.5–8](https://ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf#page=103).

Each Wasm tag lands in its existing dispatcher block. Use the retained IL
offset to select the first matching protected region in metadata order.
Handler code can lie inside another protected region, so a fresh exception
raised there can reach an enclosing catch. If no clause matches, propagate
the same tag. Each matched handler resumes after consuming the exception
operand, with independent evaluation-stack state and unchanged locals.
