# Stage 30 · Portable floating point

Run from the repository root.

    npm run demo -- stage-30-portable-floats

The Wasm-hosted compiler reads PortableReals.dll, compiles Filter, and returns 8 for a sample of 9 and three filter steps.

The portable compiler emits f32 and f64 for float and double scalar arguments, returns, local variables and evaluation-stack values. Literals copy their IEEE bit patterns directly from IL. The compiler lowers add, subtract, multiply, divide and negate, and keeps unordered comparisons correct for NaN. Mixed-width operations promote single-precision operands when the other operand is double.

Conversion behavior follows the .NET 10 CLR oracle. Unchecked casts from float and double to int, uint, long and ulong truncate and saturate, with NaN becoming zero. For an unsigned integer converted directly to float, adjacent conv.r.un and conv.r4 round once. A branch target at the conversion or an explicit intermediate conversion to double preserves the separate operation.

Run npm test to compare the same DLLs in the CLR, Node and Edge. The cases cover signed zero, infinities, NaN classification, subnormals, saturation boundaries, exact literals, and two overloads in a separately compiled dependency. NaN payloads are outside the JavaScript oracle's comparison. The self-recompiled compiler replays the same cases. Floating-point remainder and checked casts produce diagnostics at their IL offsets. Floating-point arrays, fields, small integer and native integer casts remain open.
