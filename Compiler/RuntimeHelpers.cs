// Wasm functions that implement CLR semantics with no single Wasm instruction. ModuleCompiler emits each helper once,
// and only into modules that use it, so modules stay import-free and otherwise unchanged.
internal static class RuntimeHelpers
{
    internal static readonly Dictionary<string, string> Functions = new()
    {
        // .NET evaluates double % double as C fmod: the exact truncated remainder, with the sign of x. The result is
        // always representable, and this computes it exactly with f64 operations:
        //   t := the largest |y| * 2^k <= |x|      (each doubling is exact, and an overflow to inf ends the search)
        //   for t down to |y|: if r >= t then r -= t; t /= 2
        // Invariant: before each step r < 2t, so a subtraction has t <= r < 2t and is exact (Sterbenz's lemma). Each
        // halving undoes an exact doubling. The loop runs at most about 2100 times (the exponent range plus subnormals).
        ["fmod"] = """
             (func $fmod (param $x f64) (param $y f64) (result f64)
              (local $r f64) (local $divisor f64) (local $t f64)
              ;; NaN operand, infinite dividend or zero divisor: NaN, as in C fmod and .NET.
              (if (i32.or (i32.or (f64.ne (local.get $x) (local.get $x)) (f64.ne (local.get $y) (local.get $y)))
                          (i32.or (f64.eq (f64.abs (local.get $x)) (f64.const inf)) (f64.eq (local.get $y) (f64.const 0))))
               (then (return (f64.const nan))))
              (local.set $r (f64.abs (local.get $x)))
              (local.set $divisor (f64.abs (local.get $y)))
              ;; |x| < |y|, which includes an infinite divisor and a zero dividend: x unchanged, with its sign.
              (if (f64.lt (local.get $r) (local.get $divisor)) (then (return (local.get $x))))
              (local.set $t (local.get $divisor))
              (block $grown
               (loop $grow
                (br_if $grown (f64.gt (f64.mul (local.get $t) (f64.const 2)) (local.get $r)))
                (local.set $t (f64.mul (local.get $t) (f64.const 2)))
                (br $grow)))
              (block $reduced
               (loop $reduce
                (if (f64.ge (local.get $r) (local.get $t)) (then (local.set $r (f64.sub (local.get $r) (local.get $t)))))
                (br_if $reduced (f64.eq (local.get $t) (local.get $divisor)))
                (local.set $t (f64.mul (local.get $t) (f64.const 0.5)))
                (br $reduce)))
              ;; The remainder takes the dividend's sign, so -6 % 3 is -0.
              (f64.copysign (local.get $r) (local.get $x)))
            """,
    };
}
