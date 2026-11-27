# 0092 · Multicast general delegates on the shared invocation list

Status is proposed in #256 (Stage 221). Codex allocated the stage and decision numbers and agreed
to the shared-heap design (handoffs codex-256-nominal-and-multicast-review and
codex-256-multicast-v2). It builds on decision 0091 (general delegate signatures), decision 0088
and Stage 181's EventHandler composition.

## Problem

Decision 0091 gave every general delegate key the heap-3 closure `(eqref target, funcref method)`.
That closure can't support multicast:

- `Delegate.Remove` needs method identity, and a funcref can't be compared in Wasm.
- A cast from `Delegate` or `object` needs the delegate's type, and the closure carries none.

So `+=`, `Delegate.Combine`, field-like events and `castclass` stayed diagnosed. WinForms needs all
of them. `Control.Events` (`EventHandlerList`) stores handlers as `System.Delegate`, combines them
there, and raises with `((PaintEventHandler)Events[key])?.Invoke(this, e)`.

## Decision

- **One physical list for every multicast delegate.** `EventEntry` becomes `{eqref target, (ref
  func) method, i32 method identity, i32 nominal delegate type, ref null EventHandler singleton}`.
  The heap indices stay fixed (`EventEntryHeap` 9, `EventHandlerHeap` 10).
  - The method field widens from `(ref EventThunkType)` to `(ref func)`. EventHandler invocation adds
    `ref.cast (ref EventThunkType)` before `call_ref`, which is its only change.
  - The nominal type is 0 for `EventHandler` and the key's ordinal + 1 for a general key.
- **General keys are invocation lists.** Their storage, heap type and spill bank are
  `EventHandlerHeap`'s, the same as `System.Delegate` (`DelegateReference`). A key is assignable to
  `Delegate`, so `Delegate.Combine` and `Delegate.Remove` take it. `NewDelegate` builds a one-entry
  list, as `EventWasm.NewList` does for EventHandler. Method identity is the thunk index, the same
  rule as before. Decision 0091's thunk types and signatures are unchanged.
- **Invoke.** The key's invoke helper `(list, p1..pn) -> r` calls each entry in order, casting the
  entry's method to the key's thunk type, and returns the last result. An exception propagates
  and skips the remaining entries. A list is never empty, because removing every entry yields null.
- **Combine and Remove.** These are the existing `CombineLists` and `RemoveLast`. When both operands
  are nonnull and entry 0's nominal types differ, they raise `ArgumentException`, as the CLR does for
  both. Every entry of a list shares its nominal type, so entry 0 decides. The static result type
  keeps the operands' nominal type only when both prove it (the same key, or a null literal).
  Otherwise it is `Delegate`.
- **Casts.** `castclass` and `isinst` to `EventHandler` or a key lower to `CastHandler`:
  - Null passes.
  - A value whose static type already proves the target is not checked again. This covers the
    `Combine` + `castclass` in accessors.
  - Anything else must be an `EventHandlerHeap` list (`ref.test`) whose entry 0 records the target's
    nominal type. Otherwise `castclass` raises `InvalidCastException` and `isinst` gives null.

  The check also covers the earlier unchecked `Delegate` → `EventHandler` retype, which became
  unsound once `Delegate` storage could hold general keys.
- **Field-like events.** `ldflda F; ldloc; ldloc; call CompareExchange<K>` fuses to
  `ExchangeHandlerField` for a field of key K, not only for EventHandler, when the MethodSpec's
  argument resolves to the field's own key.

## Semantics and boundaries

- Nominal identity comes from decision 0091's keys. `Notify` and `Alert` share a signature and a
  thunk type but have different keys, so a cast between them fails as on the CLR. `Action<int>` and
  `Action<uint>` stay distinct.
- `Func<int>` (decision 0088's scalar closure) and the `SpanAction<char, IntPtr>` callback keep heap
  3. They aren't `Delegate` values here, so combining one, or storing one as `object`, stays -2.
- Delegate equality (`op_Equality`, `Equals`, `GetHashCode`), `Target`, `Method`,
  `GetInvocationList` and `DynamicInvoke` aren't lowered. They reach CoreLib's bodies and are -2.
- Every entry runs on one Wasm agent, and `ExchangeHandlerField` reads and writes with no call in
  between, the same as for EventHandler.

## Verification

`npm run demo -- stage-221-multicast-delegates` runs the fixture on the x86 CLR 10.0.10 and compares
Node and Edge with it. The fixture has 21 value kernels and 3 `Reject*` kernels that must be
diagnosed. The Stage 164, 181 and 204 fixtures, self-generation and the full suite are rerun on the
stage's head.
