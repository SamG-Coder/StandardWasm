# Stage 53 · Typed null reference exceptions

Run `npm run demo -- stage-53-typed-null-access`. The generated
`ReadArray(0, -1)` returns `-102` because a null array reference raises
`NullReferenceException` before the index is checked. With a non-null array
and the same index, it returns `-101`.

The portable compiler lowers typed null failures on array length and
element access, instance field reads and writes, and string length and
character access. String character bounds use the same tag as array
bounds. The fixtures cover normal access, nested rethrow, and string
concatenation where the CLR accepts null.

Run `npm test` for CLR, Node and Edge comparisons and generated-compiler
replay. Other null-sensitive operations, including delegate invocation
and general virtual calls, require separate lowering.
