# Stage 182 · Boolean field projections

Run `npm run demo -- stage-182-boolean-field-projections` with the .NET 10
SDK, the x86 Windows runtime, Node, and Microsoft Edge. The runner uses
the same compiled .NET Standard 2.1 assembly in each runtime.

A managed Boolean field reference retains its parent value location and
field selector. The compiler resolves the closed CLI field type, reads
the packed field as an unsigned byte, and writes through the current
parent snapshot. The same managed call can also receive an ordinary
Boolean frame location.

The cases cover two references to the same struct-array field, separate
elements, whole-value replacement, independent copies, and a generic
reset through a nested Boolean alias. They also check a typed null-array
failure. Results go to
`artifacts/stage-182-boolean-field-projections/results.json`.
