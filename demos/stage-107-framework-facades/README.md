# Stage 107 · Framework facades

Run `npm test` to build the self-hosted compiler, then run
`npm run demo -- stage-107-framework-facades`. Set
`STANDARDWASM_RUNTIME_PACK` to select an installed .NET 10 runtime directory.
The runner otherwise uses the CLR's runtime directory.

The test builds unchanged C# as .NET Standard 2.0 and 2.1 DLLs. It runs the same
DLLs on the CLR and compiles their Double.IsNaN and Char.IsHighSurrogate calls in
Node and Edge through the installed netstandard 2.1 facade. It expects 20
matching values and six matching diagnostics. The original List<int> passes the constructor's deferred
initialization check and still reports an unsupported call at List.Add.

The tests reject a missing or duplicate facade, an unlisted version, a changed
key, and a non-neutral culture. The caller is responsible for choosing the
trusted runtime pack. See [decision 0038](../../docs/decisions/0038-framework-facade-version.md).
