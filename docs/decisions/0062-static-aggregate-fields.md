# 0062 · Store whole values in static fields

## Context

The original WinForms `Form` constructor reads the public init-only
`System.Drawing.Size.Empty` field. The managed field is a sequential
value type with its default value.

## Decision

Use the existing static-field binding, type initializer guards and Wasm
global operations for supported value layouts. The global holds an
immutable aggregate or the representation of the default value. A load
gets the current value and a store replaces it. A local update creates a
fresh aggregate and preserves snapshots previously read from the field.
References contained inside a copied aggregate retain their identity.

Existing access, field attribute, layout and initializer checks apply.
Taking a managed address of a static or instance field requires a
write-back path. It is outside the scope of whole-field loads and stores.

## Verification

Stage 134 compares ordinary .NET Standard DLLs with the CLR, including
initializer order, default nested values, init-only data, cross-assembly
writes, local copy updates and snapshots. A separate fixture calls the
original installed .NET 10 `System.Drawing.Size` accessors. The
unchanged WinForms replay tests how far the real startup compiles.
