# Stage 272 · Boolean storage aliases

The compiler recognizes the authenticated platform `Unsafe.As<TFrom,
TTo>` when a managed `ref bool` is viewed through a sequential value type
with one mutable Boolean field and default layout. The original location
keeps its owner and null behavior. The only admitted uses are an immediate
field read or a field write after loading one argument, local or constant.
A branch or exception-region entry into that sequence prevents lowering.

This supports the original browser Mono `Volatile.Write(ref bool, bool)`
managed body. Its internal `Unsafe.As` is lowered to the same traced
location and a packed store. The single-agent guest executes the ordered
store without handing execution to another agent. The separate readonly
`Unsafe.AsRef` used by Mono `Volatile.Read` remains outside this stage.

Run the same .NET 10 fixture on Windows x86 CLR, Node and Edge with a
frozen compiler built by `npm test`.

    npm run demo -- stage-272-boolean-storage-aliases

The Windows oracle runs against the installed x86 CLR. Node and Edge use
the genuine browser Mono CoreLib for the original volatile body. Set
`STANDARDWASM_MONO_CORELIB` when the matching package is outside the
NuGet cache. Cases cover an ordinary wrapper read and write, original
volatile writes through a class field and local, and typed null responses.
An attempted wrapper alias escape remains a compiler diagnostic.
