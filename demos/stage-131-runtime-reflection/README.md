# Stage 131 · Runtime reflection

Run `npm test` to build the self-hosted compiler, then
`npm run demo -- stage-131-runtime-reflection`. The fixture compares
ordinary compiled .NET Standard libraries on the CLR, Node and Edge.

The first cases check module and assembly identity across two assemblies,
the actual basename of renamed file-backed modules, `<Unknown>` for modules
loaded from a byte stream, and the typed exception for a null module receiver.
The resource-name fixture also compares managed strings by ordinal UTF-16
code units, including null, empty, combining marks and supplementary characters.
The unchanged WinForms startup uses these same reflection accessors.
See [decision 0059](../../docs/decisions/0059-runtime-reflection.md).
