# 0028 · Bind nested types in their declaring assembly

Proposed in [#187](https://github.com/SamG-Coder/StandardWasm/issues/187).

A nested TypeRef retains its enclosing TypeRef path. Resolve the outer
assembly and outer type first, following any type forwarder. Resolve each
child through the destination assembly's NestedClass table. Registry identity
remains the final assembly and TypeDef row, including for interface references.

Cross-assembly binding checks accessibility at every level of the path.
Internal types can bind when the destination declares the caller as a friend.
The compiler recognizes the platform InternalsVisibleTo attribute and compares
the declared name and, for a signed grant, the full metadata public key.
Strong-name signature verification and pack provenance remain host concerns.
Protected or private accessibility that requires the calling type is outside
the current binding path.

See [Stage 85](../../demos/stage-85-nested-references/README.md),
[ECMA-335, Partition II](https://ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf)
and [Microsoft's friend assembly rules](https://learn.microsoft.com/en-us/dotnet/standard/assembly/friend-assemblies).
