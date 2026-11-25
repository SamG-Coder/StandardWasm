# Stage 33 · Floating-point remainder

Run `npm run demo -- stage-33-float-remainder` from the root after `npm ci`. Expected output is `WrapDegrees(-30) = 330`.

Remainders.cs uses `%` on `double` and `float` in ordinary code: angle wrapping, the phase of a periodic signal, signed fractional parts, and a single-precision oscillator loop. The CLR evaluates floating-point `%` as C `fmod`: the exact remainder truncated toward zero, with the sign of the dividend. Wasm has no such instruction. The compiler calls a `$fmod` helper that it emits once, and only into modules that use it (`Compiler/RuntimeHelpers.cs`). The helper computes the remainder exactly with f64 operations. It finds the largest |y|·2^k ≤ |x| by exact doubling, then subtracts while halving back down to |y|. Every subtraction has t ≤ r < 2t, so it is exact (Sterbenz). `float % float` goes through the helper and is demoted, which is exact because the true remainder of two floats is a float.

| Case | CLR and Wasm result |
| --- | --- |
| NaN operand, `±∞ % y`, `x % ±0` | NaN |
| `x % ±∞` for finite x, `±0 % y` | x unchanged, keeping its sign |
| exact multiple, e.g. `-6 % 3` | zero with the dividend's sign (`-0`) |
| `0.3 % 0.1` | `0.09999999999999998` (exact fmod, not the decimal intuition) |

`npm test` compares against the same DLL on the CLR with `Object.is`. It crosses 26 values pairwise (±0, ±∞, NaN, subnormals including the smallest and largest, max/min normals, 2^53+1, values whose quotient overflows) and adds 400 random bit-pattern pairs per type. A mutation that replaces the helper with `x - trunc(x/y)*y` fails the suite.

Cost: the helper loops once per binary order of magnitude between |x| and |y|, at most about 2100 iterations for `double.MaxValue % double.Epsilon`.
