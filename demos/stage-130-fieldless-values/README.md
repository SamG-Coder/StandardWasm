# Stage 130 · Fieldless sequential values

Run `npm run demo -- stage-130-fieldless-values`. The .NET Standard assembly
contains two distinct sequential structs with declared size one and no instance
fields. The generated module creates and reads both closed generic holder types.
The expected result is 30.

`npm test` also compares the assembly on the CLR, Node and Edge. The cases cover
default and copy, a containing value, and static calls whose results are stored
into a struct field. They also check a write through a managed `ref` parameter.
Packed and nonempty declared layouts still produce diagnostics.

See [decision 0058](../../docs/decisions/0058-fieldless-sequential-values.md).
