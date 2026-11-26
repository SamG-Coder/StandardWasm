# Stage 128 · Interface dispatch

Run `npm run demo -- stage-128-interface-dispatch`. The compiler loads two
ordinary .NET Standard DLLs and emits a standalone module. A call through
`IReadable` on an explicitly mapped resource returns 63.

The CLR comparisons also cover inherited mappings, protected virtual overrides,
a hidden public method, a class that reimplements the interface, an unrelated
explicit implementation with the same name, and a typed null receiver.

The stage covers reference receivers and nongeneric interface slots. The decision
records the remaining dispatch shapes. See [decision 0056](../../docs/decisions/0056-interface-dispatch.md).
