# Stage 24 · Portable 64-bit integers

Run from the repository root.

    npm run demo -- stage-24-portable-i64

The Wasm-hosted compiler links WideLibrary.dll with WideMath.dll. The generated module exports a scalar method with an i64 argument, an i32 argument, and an i64 result. The demo calls it with 9007199254740993 and 1 and returns 95496328098598384. The method reaches two dependency overloads whose signatures differ by signedness. The linker keeps those signatures distinct, while the emitted Wasm represents both types as i64.

The portable backend implements scalar long and ulong parameters, returns, locals, constants, branches, comparisons, arithmetic, bitwise operations, shifts, and unchecked conversions. The harness encodes i64 arguments and results as signed decimal strings and uses BigInt to transport their exact bits. An unsigned result with the high bit set is exposed as a negative BigInt. JavaScript callers can recover its unsigned magnitude with BigInt.asUintN(64, result).

Run npm test to compare both the linked and single-DLL paths against the CLR in Node and Edge. The fixtures check mixed-width calls, constants beyond JavaScript's safe integer range, narrowing and widening, shifts at the width boundary, and a loop with a wide accumulator. The self-recompiled compiler repeats these cases and the earlier fixtures.

Division by zero surfaces as a Wasm trap. Stage 48 tags signed division and remainder overflow. Stage 44 covers checked arithmetic and Stage 45 covers checked narrowing. Stage 28 covers managed i64 fields and arrays.
