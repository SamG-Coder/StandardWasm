# 0006 · A managed catch for overflow

Status: proposed in [#104](https://github.com/SamG-Coder/StandardWasm/issues/104).

The compiler accepts one active catch clause whose TypeRef names the platform `System.OverflowException` and whose handler discards the initial exception operand. Its CIL control-flow check validates instruction boundaries and normal and exceptional entry. A `leave` discards the evaluation stack and leaves the protected region or handler.

A Wasm `try_table` catches the module's overflow tag around a dispatcher iteration. The dispatcher retains the throwing CIL offset until the selected instruction returns. A catch dispatches to the handler only for offsets in its protected range. A tag from another instruction propagates with its original type.

Signed divide and remainder overflow now throw the same tag as checked arithmetic and casts. Divide-by-zero remains a Wasm runtime trap. Stage 48 exercises a tagged callee as well as failures outside the try region and inside the handler.

Exception objects, inheritance-based matching, multiple clauses, filters, and finally regions require additional lowering.
