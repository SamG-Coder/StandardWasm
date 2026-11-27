# Stage 317 · Readonly ordinal string borrow

The linker recognizes the stock CoreCLR and Mono String ordinal equality caller and lends its original Ordinal.EqualsIgnoreCase call a readonly location into the packed string. The Wasm location retains the original GC owner. The borrow's readable extent is the string's code-unit count, including a zero-length extent for an empty string.

The verifier authenticates both stock metadata bodies, runs the original x86 CLR ordinal caller, rejects an unrelated string byref producer, and checks ASCII, Unicode, a surrogate pair, empty, and typed null behavior in Node and Edge with the production borrow emitter. The unchanged Mono assembly identity witness advances to Vector128<ushort>.Count in the original Ordinal.EqualsIgnoreCase body.

Set STANDARDWASM_COMPILER_PATH to the compiler image and run npm run demo -- stage-317-readonly-ordinal-string-borrow. STANDARDWASM_MONO_CORELIB can select matching stock Mono metadata.
