# 0021 · Bind instance fields across DLLs

Proposed in [#134](https://github.com/SamG-Coder/StandardWasm/issues/134).

Resolve instance field MemberRefs in the declared assembly and TypeDef.
Compare the field name and exact CLI signature before mapping the Field
row to an ordinal in the declaring class's Wasm GC layout. Distinct CLI
types can have the same Wasm storage and must remain distinct at binding.

Access uses the existing managed receiver null check. Public instance
fields with supported storage are in scope. A readonly field may be
read across a DLL boundary. Static field initialization and general
managed field addresses require separate lowering.

See [ECMA-335 metadata and instance field instructions](https://ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf).
