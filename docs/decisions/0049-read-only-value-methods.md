# 0049 · Read-only value-type instance methods

Proposed in [#259](https://github.com/SamG-Coder/StandardWasm/issues/259).

## Context

A CLI instance method on a value type has the receiver `S& this`. Callers pass
a location's address: `ldloca`, or `ldarg.0` inside another method of the
same value. [Decision 0033](0033-embedded-sequential-values.md) represents a
supported value as an immutable Wasm GC aggregate with no general managed
byrefs. [Decision 0042](0042-value-type-construction.md) admits only
constructors on a value address. The original Blazor entry calls
`TaskAwaiter::GetResult()` on `ldloca` of its awaiter.

## Decision

A non-virtual, non-constructor instance method on a supported value type is
compiled to the Wasm function type `(S this, arguments...) -> R`. Its
receiver is the location's current aggregate.

- **Body.** The receiver is read-only. `ldarg.0` may be followed only by
  `ldfld` (read the current value) or by a straight-line operand sequence
  ending in a `call` to another such method on the same address. `stfld`,
  `initobj`, a chained constructor, `stobj`, `starg.0` and every other use
  stay diagnosed, located at the callee's method and IL offset.
- **Call sites.** `call S::M` on a nonescaping `ldloca`, or on the enclosing
  value method's or value constructor's `ldarg.0`, becomes an ordinary call
  that passes the location's current aggregate. The operand scan from decision
  0042 still proves that the address goes nowhere else.

## Why this is exact

The CLR passes the address, so the callee observes the location as it is
during the call. Here the callee observes the value the location held when
the call began. These agree whenever nothing writes the location during the
call. The callee cannot, because its body is rejected if it writes through
`this`. No other code can either. The caller's address does not escape the
frame, a local is not shared across threads, and the callee receives no other
reference to it. An exception from the callee leaves the location unchanged
in both models, so protected regions need no guard.

## Still unsupported

Mutating instance methods need copy-out of the receiver. Virtual and
interface methods on value types (`constrained.` and the `Object` overrides),
primitive receivers such as `Int32::ToString`, closed generic value types and
`ldflda`, `ldelema` or `ldarga` receivers stay diagnosed.

See [Stage 121](../../demos/stage-121-value-instance-methods/README.md).
