# Stage 333 · Readonly generic character calls

A readonly UTF-16 location can flow through a statically resolved generic method with stable scalar or supported SIMD type arguments. Each signature demand names the original method and the exact closed argument list. Re-emission resolves that same specialization and checks its original IL with the stricter readonly parameter. A writable sibling keeps its own verifier type and may write to its original owner.

The fixture compares scalar and Vector128<UInt16> forwarding, writable siblings in both discovery orders, and explicit diagnostics for a readonly write or returned alias. The exact original Mono framework probe advances to its globalization P/Invoke.

Set STANDARDWASM_COMPILER_PATH to the compiler image and run npm run demo -- stage-333-readonly-generic-character-calls. STANDARDWASM_MONO_CORELIB can select CoreCLR metadata for the same fixture.
