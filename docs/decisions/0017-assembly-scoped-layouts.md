# 0017 · Assembly-scoped object layouts

Proposed in [#126](https://github.com/SamG-Coder/StandardWasm/issues/126).

Register a class by owning assembly and TypeDef row. Give each registered
class its own field slice, indexed by the offset within its Field rows.
Reserve the whole slice before resolving references so recursive layouts
can register other classes without overwriting it.

A linked dependency can use local classes, constructors, methods and
fields. The static MemberRef boundary continues to match token-free CLI
signatures. In addition to existing platform types, `System.Object` passes
the original reference using Wasm `eqref`. Resolving signatures that name
foreign classes requires nominal TypeRef resolution.

See [ECMA-335 metadata token scope](https://ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf).
