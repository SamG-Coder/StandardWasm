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

The loader also supplies the exact raw `LoadLibraryExW` binding reached by
the installed .NET 10 CoreLib. Its original managed wrapper marshals the
UTF-16 name and captures the system error in the saved P/Invoke slot.
The fixture checks case-insensitive library names, the System32 lookup flag,
error 126 for a missing library, error 87 for invalid arguments, and
successful lookup and invocation against a configured temporary directory.
A load-only module without a provider gets an empty registry and the normal
missing-library result.

The Windows .NET 10 wrapper itself is compared with its emitted Wasm. The
supported static CoreLib string marshaller reuses read-only UTF-16 pin
storage and produces address zero for a null string. The fixture verifies
the native load and indirect call share the same registry for one instance.

The trusted CoreLib `NativeLibrary_GetSymbol` QCall reads the symbol name
from guest UTF-16 memory and resolves it in the same per-instance registry.
The unchanged `GetSymbol` wrapper returns zero for a missing export when
its throwing flag is false. The unchanged public `GetExport` raises a
managed `EntryPointNotFoundException` on that miss. The verifier checks
case-sensitive resolution, a matching export token, and the managed catch.
The exception currently carries a generic missing-export message.

Public `TryGetExport` passes an `out nint` location. The x86 CLR verifies
its expected success and false results, and the compiler still reports
the unsupported location at the original managed call site. The raw QCall
and its non-throwing result already work independently of that location.
