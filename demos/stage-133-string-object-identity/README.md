# Stage 133 · String object identity

Compile two ordinary .NET Standard libraries as one Wasm module and compare
string reference identity with the same DLLs on .NET. The fixture includes
the same literal in both assemblies, a distinct literal of the same length,
empty-string producers, concatenation, and a freshly constructed string of
equal contents. It also widens to System.Object, tries both kinds of cast,
and checks the typed failure for a non-string object.

Run `npm run demo -- stage-133-string-object-identity` on Windows after building
the portable compiler. The runner uses matching .NET 10 x86 and default-host
runtimes. It runs the CLR oracle on x86 and supplies that runtime's
`netstandard.dll`, `System.Runtime.dll` and `System.Private.CoreLib.dll` to the
compiler so casts resolve the authentic `System.String` definition. Node and
Edge should match the CLR reference equality matrix, including repeated calls.
