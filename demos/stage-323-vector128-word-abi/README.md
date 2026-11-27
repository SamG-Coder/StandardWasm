# Stage 323 · Managed Vector128<UInt16> values

The compiler checks the stock CoreLib vector layout before representing a closed Vector128<UInt16> as a Wasm v128. The sixteen-byte vector contains two readonly eight-byte halves, each backed by one UInt64. A managed copy, branch-selected value, parameter or return preserves all vector bits. Default initialization supplies a zero vector.

The fixture uses the original Vector128.Create(UInt16) and GetElement<UInt16> APIs. The compiler emits i16x8.splat and i16x8.extract_lane_u for verified literal indices. Dynamic indices, out-of-range indices, other lane types, boxing, and JavaScript exports retain explicit diagnostics. Selection of the original generic SIMD method remains pending its closed-interface binding.

Set STANDARDWASM_COMPILER_PATH to the compiler image and run npm run demo -- stage-323-vector128-word-abi. STANDARDWASM_MONO_CORELIB can select CoreCLR metadata for the same fixture.
