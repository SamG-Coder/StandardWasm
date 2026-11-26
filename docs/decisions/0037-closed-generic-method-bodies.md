# 0037 · Specialize method bodies by closed declaring instantiation

Proposed in [#193](https://github.com/SamG-Coder/StandardWasm/issues/193).

A compiled function is identified by (defining assembly, MethodDef, closed
declaring layout). The closed layout is the one registered by
[decision 0029](0029-closed-generic-reference-layouts.md): resolved definition
plus ordered type arguments. Nongeneric definitions use layout zero and keep
the dense per-row index. Two instantiations of the same definition always get
distinct Wasm functions, even when their storage coincides.

The layout's arguments substitute `VAR` in the method signature, the local
signature, TypeSpec MemberRef parents and field operands. Substitution
produces the argument's storage, so `T = int` and `T = uint` both use i32.
The CLR keeps signed and unsigned semantics in the IL that consumes the value
(`div` versus `div.un`, `blt` versus `blt.un`), and the compiler preserves
that IL unchanged. `T = sbyte` and `T = byte` use distinct packed storage and
extension.

A MemberRef whose parent is a TypeSpec resolves through the closed layout
graph. The layout already proved the definition identity, type accessibility
and generic constraints. Member accessibility stays strict: public members
from any assembly. A closed parent in the caller's own assembly also admits
assembly members and private members of the caller's own definition. Nested
and friend member access are not modeled. Static fields, type initializers
and `initonly` writes stay rejected.

The function table reserves one specialized body per MethodDef row. A
program that needs more reports -3. Generic method parameters (`MVAR`,
MethodSpec), virtual and interface dispatch, value-type declaring
instantiations and open generic bodies reached without a closed declaring
type report -2. See [Stage 105](../../demos/stage-105-generic-method-bodies/README.md).
