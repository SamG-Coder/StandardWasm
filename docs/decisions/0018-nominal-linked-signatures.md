# 0018 · Resolve nominal linked signatures

Proposed in [#128](https://github.com/SamG-Coder/StandardWasm/issues/128).

A class slot can name a local TypeDef or an AssemblyRef-scoped TypeRef.
Resolve both forms to an owning bundle assembly and TypeDef row before
overload matching. Preserve exact primitive encodings and primitive
array element encodings. The Wasm emitter then uses the registered layout
for the resolved class, including foreign types in CLI locals.

Assignment remains nominal for specific class slots. This change
resolves public top-level classes in the bundle and static method
signatures. Nested or forwarded types and cross-assembly fields and
instance member references require separate lowering.

See [ECMA-335 TypeReference scope and signatures](https://ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf).
