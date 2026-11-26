# 0007 · Ordered arithmetic catches

Status: proposed in [#106](https://github.com/SamG-Coder/StandardWasm/issues/106).
Extends [0006](0006-overflow-catches.md).

The portable compiler supports sibling catches with one protected range
and separate handlers. It validates boundaries, rejects overlapping handler
ranges, and checks normal control flow across all clauses. Each handler
begins by discarding the exception operand.

Use separate module tags for checked overflow and integer divide-by-zero.
During compilation, map each tag to the first applicable clause in metadata
order. A platform `System.ArithmeticException` clause matches either tag.
Integer division guards the divisor before executing the Wasm operation.
Signed minimum divided by or reduced modulo minus one still raises the
overflow tag. Floating-point division retains IEEE behavior.

The dispatcher catches both tags and checks the throwing IL offset before
switching to the selected handler. When there is no match, including for a
failure outside the protected range, it throws the same tag. Existing Wasm
traps remain traps. Exception objects, nested regions, broader base types,
filters and finally still require managed exception lowering.
