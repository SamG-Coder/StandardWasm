# Stage 148 · Executing assembly identity

Run `npm test` to build the self-hosted compiler, then run
`npm run demo -- stage-148-executing-assembly` on Windows with the matching
.NET 10 x86 and default runtimes, Node and Edge.

Two ordinary .NET Standard libraries call `Assembly.GetExecutingAssembly`.
The comparison checks the primary assembly, a separately linked method, and
the identity selected by a branch. Each result agrees with `typeof(T).Assembly`
for the assembly that owns the executing IL. The dependency method keeps its
own assembly identity when reached from the primary library.

See [decision 0076](../../docs/decisions/0076-executing-assembly.md).
