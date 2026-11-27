# 0061 · Preserve string identity when widening to Object

## Context

The original .NET 10 WinForms startup passes a managed string to the
`object` parameter of the original CoreLib `String.Format` overload. A
string must retain the same identity when passed, stored, returned or
widened to `System.Object`.

## Decision

The immutable Wasm String struct is a managed reference. The compiler
widens it to the existing `eqref` Object representation and uses the
original String heap type for `isinst` and `castclass`. Only platform
string metadata selects that heap type. Invalid casts keep the managed
InvalidCastException path and a null cast keeps its null result.

Each emitted module interns its literal strings by exact UTF-16 content
across all linked assemblies. Literals are allocated on first access and
cached in module-owned globals. An empty literal, the platform
`String.Empty` reference, supported empty constructors and empty
concatenation share one cached String. Two-argument concatenation
preserves the identity of its nonempty argument when the other is empty
or null. A nonempty construction keeps its own identity.

This cache covers the assemblies linked into one emitted module. Managed
`String.Intern` and identity across separately instantiated modules need
a runtime-wide design when those assemblies share managed objects.

## Verification

Stage 133 compares reference identity with .NET across repeated calls
and linked assemblies in Node and Edge. It includes nonempty copies,
empty constructors, concatenation, object argument and return paths,
null casts, runtime type tests and a caught invalid cast. The original
WinForms replay checks the startup call site with the unchanged DLLs.
