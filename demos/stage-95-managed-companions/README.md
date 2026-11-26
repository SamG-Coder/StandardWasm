# Stage 95 · Original managed companion DLLs

Run `npm test` to build the self-hosted compiler, then run
`npm run demo -- stage-95-managed-companions` on Windows with the .NET 10 SDK,
Windows Desktop runtime, Node and Microsoft Edge.

The application calls a separately built .NET Standard dependency. Edge
drops each original publish through the same page and worker used by
[Stage 74](../stage-74-executable-entry/README.md). The original Windows
process, Node and Edge return 61 for the publish folder and the single-file
executable. The runner also verifies missing, invalid, wrong-identity and
duplicate managed companions.

The worker reads the managed runtime assets listed for the selected target
in the original dependency manifest. It resolves publish paths and checks
the PE format. StandardWasm binds the assemblies by CLR identity. The page
can also supply a separately selected set of framework DLLs. The runner
uses that option to send the original Stage 72 WinForms fixture and its
installed dependencies through the worker. Compilation stops with an
unsupported status before the first window opens.

This stage covers managed runtime assets for the selected manifest target,
a parameterless integer entry, and import-free output. It does not implement
RID fallback resolution, satellite resources or native dependency execution.
The generated compiler on this branch reports a status code. A compiler
that exports detailed diagnostics also reports the assembly, method row
and IL offset. Set `STANDARDWASM_COMPILER_PATH` to test another generated
compiler. Results and the compiler hash are under
`artifacts/managed-companions/run-*/results.json`.
