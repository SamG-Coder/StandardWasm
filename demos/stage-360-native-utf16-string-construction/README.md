# Stage 360 · Native UTF-16 string construction

The exact CoreLib String(char*, int, int) internal call now binds to its private managed Ctor. The compiler retains the original length, start and pointer checks. Its authenticated fresh-string allocation and Memmove<char> sequence shares the existing native span snapshot emitter with a full linear-memory extent check.

The fixture compares the UTF-16 snapshot and canonical zero-length string in x86 CLR, Node and Edge. It verifies the x86 constructor guard priority and preserves explicit compiler diagnostics for dependencies in the CoreLib exception paths. An out-of-memory-range guest span traps before reading. The constructor's end-to-end Wasm execution still depends on the original exception graph.

The original Mono assembly identity witness passes constructor resolution and reaches the caller's earlier pinned local validation in CultureData.JSGetLocaleInfo. On this branch, the full unchanged Blazor publish reaches its separate constrained ToString dependency in DefaultInterpolatedStringHandler.AppendFormatted<T>.

Set STANDARDWASM_COMPILER_PATH to the compiler image and run npm run demo -- stage-360-native-utf16-string-construction. STANDARDWASM_MONO_CORELIB selects the runtime metadata used for linking.
