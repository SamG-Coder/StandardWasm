# 0014 · Instance receiver checks

Proposed in [#120](https://github.com/SamG-Coder/StandardWasm/issues/120).

Use the decoded callvirt marker to emit the managed null check on known
same-module instance calls. The receiver and argument spills already hold
the evaluated values. Direct call and newobj keep their existing lowering.

The supported event compare-exchange pattern fuses an instance field
address with side-effect-free argument loads and the intrinsic. Keep
the field address's managed null check at the fused instruction.

Both failures use the existing `NullReferenceException` tag so enclosing
catch and finally blocks observe them. General virtual dispatch and
managed interior references remain separate work.

See [CLI callvirt](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.callvirt?view=net-10.0)
and [CLI ldflda](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.ldflda?view=net-10.0).
