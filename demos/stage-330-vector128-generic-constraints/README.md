# Stage 330 · Closed Vector128 generic constraints

A proven Vector128<UInt16> SIMD slot satisfies the nonnullable value constraint. The compiler checks closed interfaces through the original CoreLib InterfaceImpl records and substitutes UInt16 for the declaring lane. Managed generic methods retain the original method body and Wasm v128 values.

The ordinary fixture compares a value constraint, a self-referential interface constraint, an explicitly named closed vector interface and a primitive control. The unchanged Mono ordinal call now passes the original closed SIMD-interface proof. Its readonly character arguments still need signature propagation to the specialized callee.

Set STANDARDWASM_COMPILER_PATH to the compiler image and run npm run demo -- stage-330-vector128-generic-constraints. STANDARDWASM_MONO_CORELIB can select the CoreCLR metadata profile.
