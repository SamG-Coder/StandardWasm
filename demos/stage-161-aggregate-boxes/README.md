# Stage 161 · Aggregate boxes

Run `npm run demo -- stage-161-aggregate-boxes --no-rebuild` after `npm test`
has produced a compiler. Set `STANDARDWASM_COMPILER_PATH` to verify a different
frozen compiler. The runner submits the same .NET Standard 2.0 assembly to the
compiler in Node and Edge, then compares the generated programs with the
Windows x86 CLR oracle. Hashes and outcomes go to `artifacts/stage-161-aggregate-boxes/`.

The fixture caches a boxed value across ordinary managed calls and checks that
both the original value and an unboxed copy can change without changing the
cached value. It distinguishes two boxes of the same value, a box of the
default value, and structs with identical field layouts. It checks nominal
reflection and concrete or interface type tests, retains a reference field through boxing, and
compares the typed exceptions for a null input and incompatible boxes.

Expected outcomes in fixture order are `192360`, `118`, `13`, `37`, `41`,
`43`, `47`, `53`, and `59`.

See [decision 0087](../../docs/decisions/0087-aggregate-boxes.md).
