# 0036 · Primitive generic argument layout

A closed generic type keeps each primitive argument's CLI element identity
until the linker matches the complete type. The layout reader projects
the argument into its Wasm field representation only when that field,
local or parameter needs storage. As a result, `Pair<int, Label>` and
`Pair<uint, Label>` share a field width and retain distinct type identities.
A primitive vector argument also keeps its exact element identity.
Named primitive types resolve through recognized platform metadata. The
layout can share the same primitive representation, while field linkage
preserves the CLR distinction between a named argument and its short encoding.

Start closed value-type instances with the existing value default
representation. Resolve field MemberRefs whose parent is a closed
TypeSpec against the declaring layout, comparing the field signature in
that instantiation. Updating a local uses the existing immutable aggregate
copy operation. A layout can include a vector of closed value types.
Array element addresses and mutation of generic value elements still
require their own lowering. Closed generic method and constructor bodies
are tracked separately.

[Stage 96](../../demos/stage-96-primitive-generic-layouts/README.md) verifies
the same applications and unsigned identity mismatches on the CLR and Wasm.
