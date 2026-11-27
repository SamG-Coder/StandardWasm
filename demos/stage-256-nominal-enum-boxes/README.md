# Stage 256. Nominal enum boxes

Run `npm run demo -- stage-256-nominal-enum-boxes` to compare the same
.NET Standard fixture on Windows x86 CLR, Node and Edge.

Boxing preserves the exact enum identity for type tests and GetType.
The scalar payload retains the original integer element type for checked
unboxing, including the CLR's matching underlying-type conversions.
The fixture covers object identity, signedness, narrow and wide payloads,
nullable object input, and CoreLib interface membership.

Virtual slots on boxed enums and managed addresses returned by `unbox`
require separate implementations. The stage checks the diagnostic for the
original virtual call when that dispatch can receive a boxed enum.
