# Stage 165 · Dynamic native calls

Run `npm run demo -- stage-165-dynamic-native-calls` with the .NET 10 SDK, the
x86 Windows runtime, Node, and Microsoft Edge. Set `STANDARDWASM_COMPILER_PATH`
to use a specific frozen compiler. Results are written to
`artifacts/stage-165-dynamic-native-calls/results.json`.

The CLR oracle runs the original C# fixture against two unmanaged callbacks.
The Wasm fixture uses the same assembly and the browser dynamic library
registry. The same call site selects either export token. Both engines match
the CLR's signed argument, 32-bit unsigned result, UTF-16 buffer write,
unchanged neighboring code unit, and null data pointer value.

The verification also checks stale token rejection and diagnostics located
at an unsupported signed-result signature and at a fixed managed character
reference. The original C# code can pin that reference on Windows. The
browser pinning implementation for that location remains pending.
