# Stage 46 · Typed checked overflow

Run `npm run demo -- stage-46-typed-overflow`. The generated `AddOrDivide(0, 2147483647, 1)` throws the overflow tag. The runner checks tag identity before accepting the expected failure.

Standalone modules with reachable checked arithmetic or fixed-width checked conversions define their own WebAssembly exception tag. Its export name is `"\u0000overflow"`. The leading NUL reserves the name outside the compiler's printable manifest namespace. A host can use `managedExceptionType(instance.exports, error)` from `scripts/invoke.mjs` to identify `System.OverflowException` by tag identity. Each instance owns its tag.

`npm test` distinguishes typed overflow from ordinary Wasm traps. The fixtures include signed multiplication at the minimum integer boundary, unsigned carry, NaN and out-of-range conversions, and divide-by-zero in a module that also exports an overflow tag. Managed CIL exception handlers and managed exception objects remain open.
