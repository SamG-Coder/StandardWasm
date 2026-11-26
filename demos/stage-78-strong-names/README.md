# Stage 78 · Strong-named assembly identity

Run `npm run demo -- stage-78-strong-names` and `npm test`.
The main .NET Standard DLL calls a separate public-signed DLL using its
strong-name identity. The fixture keys are public only. The test also
builds an alternate assembly with the same name and version and another
key. For CLR comparisons it uses the original dependency.

The linker compares a full reference public key directly, or derives the
CLR public-key token from the stored definition key. It retains exact names
and versions, neutral culture, and duplicate-identity diagnostics. Tests
cover both key encodings, a different key, dependency ordering, and
unchanged output after rejection. The token calculation is metadata identity
matching. It is not signature or publisher verification.

Framework pack selection and CLR version roll-forward are separate work.
See [Microsoft's AssemblyName documentation](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.assemblyname?view=netstandard-2.0).
