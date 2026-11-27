# Stage 166 · Span field reader arguments

Run `npm run demo -- stage-166-span-field-reader-arguments` with the .NET 10
SDK, the x86 Windows runtime, Node, and Microsoft Edge. The runner compares the
same .NET Standard 2.1 assembly on the CLR and both Wasm engines.

The fixture reads and writes a `Span<char>` field and takes a slice when an
ordinary instance method supplies an argument. The compiler verifies that
the method body reads a signed Int32 field on the same declaring type before
using a descriptor snapshot. The original method call still runs. Span
element writes and the resulting slices share the backing array.

The runner checks a typed bounds failure and verifies that a helper that
replaces the span descriptor still produces a located compiler diagnostic.
The CLR oracle confirms that this last case writes to the replacement
array. Results go to
`artifacts/stage-166-span-field-reader-arguments/results.json`.
