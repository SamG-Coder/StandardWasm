# Stage 45 · Checked fixed-width numeric casts

Run `npm run demo -- stage-45-checked-casts`. The portable compiler reads the .NET Standard library and emits a Wasm export. `F64ToU8(255.9)` returns `255`.

The compiler handles all fixed-width checked conversion opcodes for integer results of 8, 16, 32, and 64 bits. It respects signed and unsigned source bits and checks bounds before narrowing integer inputs. Floating-point inputs truncate toward zero. NaN, infinities, and range errors raise a CLR overflow exception and throw the Wasm tag documented in Stage 46.

`npm test` compares signed and unsigned integer inputs, adjacent floating-point values at the limits, fractional edges, signed zero, and subnormals. Native-sized checked conversions and managed exception handlers remain open.
