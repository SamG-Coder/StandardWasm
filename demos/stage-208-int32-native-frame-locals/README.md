# Stage 208 · Int32 native frame locals

An address-converted CLI `int32` or `uint32` local uses a stable four-byte cell in the existing linear shadow stack. Normal local reads and writes use the same bytes as native pointer operations. The declared local signature separates four-byte integers from narrower values that share the same evaluation-stack type. The shadow-stack allocator zeroes the cell on entry and releases it when its frame exits.

Run `npm run demo -- stage-208-int32-native-frame-locals`. Supply a prebuilt compiler with `STANDARDWASM_COMPILER_PATH` and `--no-rebuild` to pin verification.

The same .NET Standard 2.0 DLL checks signed and unsigned aliasing, a pointer write in a separate method, distinct cells, and repeated frame reuse. Mixed native and managed addresses to a local, `bool` and `short` have located diagnostics in Node and Edge.
