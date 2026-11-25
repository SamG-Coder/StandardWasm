# Stage 09 · Portable emitter

Run the following from the repository root after npm ci.

    npm run demo -- stage-09-portable-emitter

The bootstrapped compiler runs inside Wasm. It reads EmitterLibrary.dll, finds DemoKernel in its MethodDef table, and emits a fresh Wasm module. The host instantiates the returned bytes and invokes run(11). The result is 40.

The portable compiler supports a single static, straight-line method with i32 arguments and an i32 result. It decodes the CLI signature and body, validates stack movement, lowers integer constants, loads, arithmetic, bitwise operations, and shifts, and emits a binary module with no imports. It reports unsupported IL, signatures, locals, and extra sections. Methods calling other methods or depending on type initialization are outside this stage. The export name is run.

The test compares emitted bytes with those produced by the same portable compiler on .NET. It then compares execution with the original C# methods on the CLR. Node and Edge both run the bootstrapped compiler and instantiate its output. JavaScript transfers buffers and invokes exports. It does not compile the input IL.

This demonstrates an executable compiler inside Wasm. Compiling the compiler itself requires support for its control flow, calls, locals, arrays, and whole-module linking.
