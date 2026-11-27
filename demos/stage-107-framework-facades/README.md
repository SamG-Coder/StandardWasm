# Stage 107 · Framework facades

Run `npm test` to build the self-hosted compiler, then run
`npm run demo -- stage-107-framework-facades`. The oracle
runs on the installed win-x86 .NET 10 CLR, because the compiled calls lower
original CoreLib bodies that must come from a 32-bit CoreLib
([decision 0044](../../docs/decisions/0044-corelib-native-width.md)). Set
`STANDARDWASM_RUNTIME_PACK` to select another installed .NET 10 runtime
directory. The runner otherwise uses the x86 CLR's runtime directory.

The test builds unchanged C# as .NET Standard 2.0 and 2.1 DLLs. It runs the same
DLLs on the CLR and compiles their `Double.IsNaN`, `Char.IsHighSurrogate`, and
`List<int>` calls in Node and Edge through the installed netstandard 2.1 facade.
The list fixture covers first use, repeated capacity growth, and reads after
resizing. Both target frameworks contribute to 32 CLR comparisons.

The tests reject a missing or duplicate facade, an unlisted version, a changed
key, and a non-neutral culture. The caller is responsible for choosing the
trusted runtime pack. See [decision 0038](../../docs/decisions/0038-framework-facade-version.md).
