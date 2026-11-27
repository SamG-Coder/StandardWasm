# Stage 320 · Closed Vector128 lane counts

The compiler evaluates the authentic Vector128<T>.Count getter for the twelve supported lane identities. The fixed 16-byte width gives 16, 8, 4 or 2 lanes. Native integers use four bytes on wasm32. Calls through a closed generic method retain the concrete lane identity, and a same-named type in another assembly follows its own getter.

The x86 CLR rejects Char, Boolean and enum lanes with NotSupportedException. These calls remain explicit compiler diagnostics while the original resource-specific exception path is pending. The supported count operation is independent of the vector execution ABI and hardware capability getter.

Set STANDARDWASM_COMPILER_PATH to the compiler image and run npm run demo -- stage-320-vector128-count. The verifier compares one ordinary DLL on x86 CLR, Node and Edge with the stock browser Mono metadata. STANDARDWASM_MONO_CORELIB can select another exact CoreLib image.
