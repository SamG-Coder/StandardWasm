# Stage 133 · String object identity

Compile two ordinary .NET Standard libraries as one Wasm module and compare
string reference identity with the same DLLs on .NET. The fixture includes
the same literal in both assemblies, a distinct literal of the same length,
an empty literal, and a freshly constructed string of equal contents.

Run `npm run demo -- stage-133-string-object-identity` after building the
portable compiler. Node and Edge should match the CLR reference equality
matrix, including repeated calls.
