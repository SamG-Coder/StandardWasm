# 0034 · Keep enum signatures nominal

Proposed in [#202](https://github.com/SamG-Coder/StandardWasm/issues/202).

An enum binds by the defining assembly and TypeDef. The reader verifies its
platform System.Enum base and the declared `value__` field. Only the
underlying integer storage enters the Wasm field layout, frame, or evaluation
stack. Mutable fields and method signatures still match by the full enum
identity before the compiler lowers them.

Backed values use the existing integer widths and packed loads. CLI bitwise
operations and conversions operate on the backing representation. This
decision covers unboxed enum values. Enum reflection, boxing and native ABI
layout retain separate semantics.

One-dimensional enum vectors retain the enum definition as part of the
vector's Wasm type. Their mutable elements use the declared backing width,
including packed i8 and i16 cells, and ordinary integer element operations
use the same signed and unsigned loads as primitive vectors. Typed element
addresses and reflection require separate support. The [executable-entry
regression](../../demos/stage-74-executable-entry/README.md) compares allocation,
zero defaults, element reads and writes, and typed failures against the CLR.

See [Stage 92](../../demos/stage-92-nominal-enums/README.md) and
[ECMA-335, Partitions I and II](https://www.ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf).
