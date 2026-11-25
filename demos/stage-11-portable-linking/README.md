# Stage 11 · Portable method linking

From the repository root after installing dependencies:

    npm run demo -- stage-11-portable-linking

The compiler running in Wasm reads LinkLibrary.dll and compiles CombinePaths and its static callees. The generated module returns 96 for input 12.

The linker discovers same-assembly MethodDef calls in IL order, assigns each callee a stable Wasm index and validates its static i32 signature. A method is registered before traversing its body so shared callees and recursive edges reuse one function. The binary has one exported entry point and private Wasm functions for callees. The compiler rejects unsupported initialization, external calls, malformed tokens and incompatible stack effects.

The suite checks that the shared-callee example emits five functions and the mutually recursive example emits three. Node and Edge instantiate the Wasm emitted by the compiler running inside Wasm, compare it byte for byte with the same compiler running on .NET, and compare execution with the original C# methods.
