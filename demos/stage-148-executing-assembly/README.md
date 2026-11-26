# Stage 148 · Executing assembly identity

Run `npm test` to build the self-hosted compiler, then run
`npm run demo -- stage-148-executing-assembly` on Windows with the matching
.NET 10 x86 and default runtimes, Node and Edge.

Two ordinary .NET Standard libraries call `Assembly.GetExecutingAssembly`.
The comparison checks the primary assembly, a separately linked method, and
the identity selected by a branch. Each result agrees with `typeof(T).Assembly`
for the assembly that owns the executing IL. The dependency method keeps its
own assembly identity when reached from the primary library.

Set `STANDARDWASM_WINFORMS_REFERENCE` to a completed
[Stage 72](../stage-72-winforms-windows-reference/README.md) run to replay both
original WinForms publish formats. The runner verifies the Windows file hashes,
then compares compilation in Node and through the Edge drop launcher. With the
matching x86 Windows Desktop runtime, both should reach `String.TrimEnd()` at
IL `002c` before opening a window. It writes `winforms-startup.json` beside the
CLR comparison results under `artifacts/executing-assembly/`.

See [decision 0076](../../docs/decisions/0076-executing-assembly.md).
