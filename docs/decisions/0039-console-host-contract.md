# 0039 · Console host contract

An original console app reaches the browser host through two contracts: one for the arguments the host passes to `Main(string[] args)`, and one for the bytes the app writes to stdout and stderr. Both contracts are explicit. A module that uses neither keeps the existing invariant of having no imports.

## Output imports

A module imports these functions only when it calls a supported Console member:

| Module | Field | Type | Meaning |
| --- | --- | --- | --- |
| `standardwasm:console` | `write` | `(i32 stream, i32 codeUnit) -> ()` | Append one UTF-16 code unit |
| `standardwasm:console` | `newLine` | `(i32 stream) -> ()` | Append `Environment.NewLine` |

Stream 1 is standard output and stream 2 is standard error. Both imports are function indices 0 and 1. The compiler can find Console use late in linking. When it does, it compiles the program again with both indices reserved, so every emitted call, export, thunk and element index includes the offset.

The module passes code units in order and never encodes them. The host owns what the operating system owns for the CLR: `Console.OutputEncoding` and `Environment.NewLine`. It encodes each stream once, at the end, which matches the CLR's stateful encoder when a surrogate pair is split across two `Write` calls. The launcher models two encodings:

- **UTF-8.** There is no preamble, and each unpaired surrogate becomes U+FFFD.
- **Windows-1252.** Characters with an exact byte map to it, and each surrogate code unit becomes `?`.

Any other character throws an error naming it. That covers the characters .NET would send through Windows best-fit tables, which the launcher doesn't model.

Measured on Windows 11 with .NET 10.0, a console publish whose stdout is a pipe encodes with the console code page (437 when it inherits a console). Without a console, it encodes with the ANSI code page (1252 here). In both cases it writes `??` for a non-BMP character. A comparison therefore has to name its configuration. Stage 110 starts the Windows reference without a console and reads the ANSI code page from the registry. It stops if there is no modeled encoding for that code page.

## Recognized members

These members are recognized only when the parent TypeRef is `System.Console` or `System.IO.TextWriter` in an authentic platform assembly. `System.Console` must come from the `System.Console` assembly or from the `netstandard` or `mscorlib` facade. `System.IO.TextWriter` must come from `System.Runtime`, `System.Private.CoreLib`, `netstandard` or `mscorlib`. In every case the name and public key token must match (`PlatformAssemblyIdentity`).

| Member | Required IL | Lowering |
| --- | --- | --- |
| `Console.get_Out()`, `Console.get_Error()` | `call` | Push a stream handle typed `ConsoleStorage.Writer` (i32 1 or 2) |
| `TextWriter.Write(string)` | `callvirt` | Write each code unit; a null string writes nothing |
| `TextWriter.WriteLine(string)` | `callvirt` | Same as `Write`, then `newLine` |
| `TextWriter.WriteLine()` | `callvirt` | `newLine` |

The handle type exists only on the evaluation stack. A `TextWriter` local, field, parameter or array still goes through the ordinary class linker, which has no TextWriter layout and rejects it. A non-virtual `call` to a write method would bypass the console's synchronized writer, so it is rejected too.

Any other member of Console or TextWriter fails with status -2. `CompileDetailed` reports the assembly, the MethodDef row and the IL offset. Examples are `Console.WriteLine`, `Console.Read`, `TextWriter.Flush` and `Write(char)`. `Console.ReadLine` and interactive stdin were added by decision 0079 (Stage 151).

## Arguments

A host can't build Wasm GC objects itself. When an export takes a `string[]`, the module also exports four constructors. The names start with NUL so they can't collide with CLR method names.

| Export | Type |
| --- | --- |
| `\0newArguments` | `(count i32) -> String[]` |
| `\0newString` | `(length i32) -> Char[]` |
| `\0setStringCodeUnit` | `(Char[], index i32, codeUnit i32) -> ()` |
| `\0setArgument` | `(String[], index i32, Char[]) -> ()` |

`setArgument` wraps the `Char[]` in an immutable String. The host must not write to that `Char[]` afterwards. The arrays are checked by Wasm instructions, so a bad index or a negative length traps. No managed code runs during construction, so no CLR exception is lost. The entry export `run` then takes the finished array.

A `void Main` exits with status 0, and `Environment.ExitCode` is an unsupported member. When an unhandled managed exception escapes, the launcher reports `unhandled-exception`. It doesn't reproduce the CLR report or the Windows exit status.

## String vectors

`string[]` is an ordinary reference vector whose elements are the immutable String struct, nullable. `newarr System.String` matches the platform String TypeRef in the same way. `ldelem.ref`, `stelem.ref` and `ldlen` use the existing reference-vector lowering, with its typed null and bounds exceptions. String is sealed, so a store into a `string[]` can't fail the CLR covariance check.

[Stage 110](../../demos/stage-110-console-arguments/README.md) compares the Windows processes, Node and Edge.
