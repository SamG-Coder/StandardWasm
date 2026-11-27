# Stage 232 · Obsolete static fields

Run `npm run demo -- stage-232-obsolete-static-fields` with the Windows
x86 CLR, Node and Edge. The same .NET Standard DLL verifies static
initialization, alias-visible character changes and scalar updates.

Authentic `System.ObsoleteAttribute` describes source-level deprecation.
The compiler uses the usual typed field storage and initialization for
these already-compiled accesses. An unrelated attribute with the same
short name still produces a located diagnostic. The original .NET 10
`System.IO.Path.InvalidPathChars` field uses the same handling.
