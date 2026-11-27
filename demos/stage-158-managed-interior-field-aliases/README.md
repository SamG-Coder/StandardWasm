# Stage 158 · Managed interior field aliases

Run `npm run demo -- stage-158-managed-interior-field-aliases --no-rebuild`
after `npm test` has produced a self-hosted compiler. Set
`STANDARDWASM_COMPILER_PATH` to exercise another frozen compiler. The runner
builds the ordinary .NET Standard 2.0 fixture, records its results on Windows
x86 .NET 10, and submits the same DLL to the compiler running in Node and
Edge. It executes the resulting import-free Wasm and records the compiler
and fixture hashes, runtime versions, and results under
`artifacts/stage-158-managed-interior-field-aliases/`.

The fixture takes addresses into generic value types stored in arrays and
class fields. It passes addresses through a generic method return and
reuses the same dereference site for a projected field and an ordinary
array element. The nested cases replace the containing entry, update a
child through an existing reference, and inspect the original value copy,
sibling fields, and reference identity. The case with a two-field object
and integer payload exercises a value layout shaped like a location.

The expected results in fixture order are `5923`, `10503`, `1220`, `10308`,
`44246`, `11134`, and `47`.

See [decision 0085](../../docs/decisions/0085-managed-interior-field-aliases.md).
