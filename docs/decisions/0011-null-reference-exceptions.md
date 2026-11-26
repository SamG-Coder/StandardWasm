# 0011 · Null reference exceptions

Proposed in [#114](https://github.com/SamG-Coder/StandardWasm/issues/114).

Test the reference with Wasm `ref.is_null` before dereferencing it in
portable array, instance field and string operations. Emit the
module-owned `NullReferenceException` tag on a null reference. Array and
string indexing check the index only after the null check. String
indexing uses the existing `IndexOutOfRangeException` tag.

String concatenation deliberately accepts null. It keeps its existing
null-to-empty conversion. Direct calls and other null-sensitive
operations retain their existing lowering.

See the CLR documentation for [array length](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.ldlen?view=net-10.0)
and [instance field access](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.ldfld?view=net-10.0).
