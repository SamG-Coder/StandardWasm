# 0013 · Allocation and delegate exceptions

Proposed in [#118](https://github.com/SamG-Coder/StandardWasm/issues/118).

For `newarr`, compare the evaluated length with zero using signed
comparison before passing it to the Wasm GC allocation instruction.
A negative value raises the existing `OverflowException` tag. Wasm host
allocation limits and out-of-memory behavior remain engine-dependent.

For supported delegate invocations, check the delegate's reference in
its spill slot. CLI argument instructions have already executed at that
point, so their side effects and exceptions precede the null check.
Event dispatch and scalar closures use the same `NullReferenceException`
tag available to enclosing catch and finally blocks.

See [CLR newarr](https://learn.microsoft.com/dotnet/api/system.reflection.emit.opcodes.newarr)
and [C# delegate invocation](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/expressions#128104-delegate-invocations).
