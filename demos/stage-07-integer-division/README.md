# Stage 07 · Integer division boundaries

Run `npm run demo -- stage-07-integer-division` from the root after `npm ci`. Expected output is `Divide(-7, 2) = -3`, because signed division truncates toward zero.

Quotients.cs exposes the four IL division instructions (`div`, `div.un`, `rem`, `rem.un`). Each appears with variable operands and with constant divisors -1, 7, 8 and 10. The file also checks the quotient/remainder identity and sums the decimal digits of an unsigned value in a loop. `npm test` compares every exported method with the same DLL on the CLR. It crosses 15 boundary values (including `int.MinValue`, -1, 0 and the unsigned high-bit patterns) with each other, then adds 200 seeded random pairs.

| Input class | CLR | Wasm (V8 message) |
| --- | --- | --- |
| Any zero divisor | `DivideByZeroException` | trap `divide by zero` / `remainder by zero` |
| `int.MinValue / -1` | `OverflowException` | trap `divide result unrepresentable` |
| `int.MinValue % -1` | `OverflowException` | trap `unreachable` from the compiler's explicit guard. Plain `i32.rem_s` would return 0. |
| All other inputs | value | identical value |

Limitation: Wasm traps are not typed CLR exceptions. A module cannot catch them, and the harness only checks that a trap happened. It reports the exception type next to the trap message. Typed exceptions are open work.
