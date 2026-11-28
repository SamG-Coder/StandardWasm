# Stage 282 · Readonly static value locations

A managed address of a readonly, non-generic aggregate static field refers to the same cell before and after a declaring type assigns that field. Ordinary loads preserve a value snapshot. The same FieldDef is used when another assembly accesses the field through a MemberRef.

Run `npm run demo -- stage-282-readonly-static-value-location` to compare identical fixture assemblies on the Windows x86 CLR, Node and Edge. Set `STANDARDWASM_COMPILER_PATH` to select a frozen compiler. The fixtures cover a default containing a null reference, a declaring initializer, snapshot isolation, linked fields, and an alias obtained before the declaring initializer's write.
