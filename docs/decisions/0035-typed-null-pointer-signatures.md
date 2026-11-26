# 0035 · Typed null pointer signatures

## Decision

Extend the [Stage 89 pointer storage](0032-unmanaged-void-pointer-fields.md)
to CLI pointers whose pointees are supported primitives or named value
types. Allow several pointer levels. A pointer slot still holds only null
using the Wasm GC nullable bottom reference type. Preserve the number of
pointer levels and the terminal primitive encoding when matching a field
or method reference. Resolve named pointees to their original assembly and
TypeDef. Binding an opaque native value type needs its definition identity.
Its field layout is irrelevant to null pointer storage.

This lets the linker represent declarations such as the `GpFont*` field
in the original `System.Drawing.Font` metadata. It leaves native address
creation, dereferencing, function pointers, pointer exports, custom modifiers
on pointees and native calls as explicit unsupported cases. A browser drawing
adapter will need a separate model for non-null handles and their lifetime.

## Evidence

[Stage 93](../../demos/stage-93-typed-pointer-fields/README.md) compares
normal compiled libraries with the CLR. The fixture distinguishes named
pointees of equal size, and tests primitive and nested pointers, default
values, linked stores and calls, and diagnostics.
