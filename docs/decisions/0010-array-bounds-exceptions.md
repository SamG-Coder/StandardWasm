# 0010 · Array bounds exceptions

Status proposed in [#112](https://github.com/SamG-Coder/StandardWasm/issues/112).

Check each portable array element access against the actual array length.
An unsigned comparison checks both a signed negative index and the upper
bound. Accesses to a null array still trap at the length instruction.
Invalid allocation lengths, string indexing and other runtime checks
remain separate lowering work.

Use one module-owned tag for the exact platform
`System.IndexOutOfRangeException`. The shared ordered catch dispatcher
handles the new tag and uses exception references for propagation and
rethrow. Arithmetic catches continue to match only arithmetic tags.

See [ECMA-335 array instructions](https://ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf).
