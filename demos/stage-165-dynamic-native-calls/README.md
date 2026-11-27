# Stage 165 Â· Dynamic native calls

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
at an unsupported signed-result signature and at a fixed pointer passed
through a managed helper. The compiler supports a proven fixed character
alias used by one direct synchronous indirect call. The fixture uses one
call site for an offset into a managed array and a stack buffer. It checks
writes through the original alias, the untouched neighboring units, and
recovery after a provider rejects a scoped call.

The executable loader resolves the indirect import only when trusted host code
passes a `configureNativeLibraries` callback in the optional platform
configuration for `runManagedEntry`. The callback receives the new instance's
Wasm memory, Win32 error slots, and registration and lookup functions. It may
register exports backed by a provider, such as a mounted virtual temporary
directory. Each launch creates its own registry.

The loader probe checks mixed direct and indirect imports, identical access
to the shared error slots, a configured temp-path export, wrong Wasm import
types, and absent configuration. Resolving native exports from original
CoreLib calls still requires the managed native-library lookup bindings.
