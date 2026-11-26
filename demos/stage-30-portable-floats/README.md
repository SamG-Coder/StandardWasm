# Stage 30 · Portable floating point

Run from the repository root.

    npm run demo -- stage-30-portable-floats

The Wasm-hosted compiler reads PortableReals.dll, compiles Filter, and returns 8 for a sample of 9 and three filter steps.

The portable compiler emits f32 and f64 for float and double scalar arguments, returns, local variables and evaluation-stack values. Literals copy their IEEE bit patterns directly from IL. The compiler lowers add, subtract, multiply, divide and negate, and keeps unordered comparisons correct for NaN. Mixed-width operations promote single-precision operands when the other operand is double.

Conversion behavior follows the .NET 10 CLR oracle. Unchecked casts from float and double to int, uint, long and ulong truncate and saturate, with NaN becoming zero. For an unsigned integer converted directly to float, adjacent conv.r.un and conv.r4 round once. A branch target at the conversion or an explicit intermediate conversion to double preserves the separate operation.

On the .NET 10 oracle, unchecked casts to byte or ushort truncate and saturate to signed 32-bit, then retain the low 8 or 16 bits. The fixture methods expose these results as int values. For example, `(byte)300.7` returns 44. .NET 11 changes direct casts to saturate at the small destination's bounds. [CLR conversion behavior](https://learn.microsoft.com/en-us/dotnet/core/compatibility/jit/11/fp-to-small-integer)

Floating-point remainder emits one private Wasm helper when a method needs it. The helper scales the divisor by exact powers of two and subtracts it in descending order. It preserves negative zero and returns NaN for invalid operands. Single-precision operands share the helper and round to f32 at the result. The direct audit also compiles the unmodified [Stage 33 DLL](../stage-33-float-remainder/README.md).

Run npm test to compare the same DLLs in the CLR, Node and Edge. The cases cover signed zero, infinities, NaN classification, subnormals, narrow and wide conversion boundaries, exact literals, and two overloads in a separately compiled dependency. The self-recompiled compiler replays these cases. Stage 45 covers checked fixed-width casts, including checked byte conversions. Native-sized casts remain open.
