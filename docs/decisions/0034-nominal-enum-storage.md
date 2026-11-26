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

See [Stage 92](../../demos/stage-92-nominal-enums/README.md) and
[ECMA-335, Partitions I and II](https://www.ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf).
