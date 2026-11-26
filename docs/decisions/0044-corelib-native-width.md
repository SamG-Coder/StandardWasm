# 0044 · CoreLib native width

Proposed in [#233](https://github.com/SamG-Coder/StandardWasm/issues/233).
Extends [decision 0027](0027-native-integer-width.md).

The browser target lowers native integers to i32. dotnet/runtime builds
System.Private.CoreLib separately for each target, so its IL already contains
that target's native width. In the 10.0.10 packs, x64 `IntPtr.get_Size` is
`ldc.i4 8; ret` and x64 `IntPtr.get_MaxValue` is `ldc.i8 0x7fffffffffffffff; conv.i`.
Lowering that body into i32 returns -1. The x86 and browser-wasm bodies are
`ldc.i4 4; ret` and `ldc.i4 0x7fffffff; conv.i`.

## Rule

The compiler lowers a method body from a System.Private.CoreLib definition
only when that CoreLib's own native width is 4. Otherwise the compile stops
with status **-7**. The diagnostic location gives the CoreLib's bundle index,
the MethodDef row that would have been lowered, and IL offset -1. The host maps
the bundle index to the runtime pack it supplied.

The compiler reads the width from the CoreLib's single public static
`System.IntPtr.get_Size()` returning int32. The body must be exactly
`ldc.i4.4`, `ldc.i4.8`, `ldc.i4.s N` or `ldc.i4 N`, followed by `ret`, with no
locals or exception sections. N must be 4 or 8. A missing, duplicated or
differently shaped body is treated as an unknown width and gets the same
diagnostic. The compiler doesn't infer a width it can't read.

The PE header isn't used to decide the width. The browser-wasm CoreLib is an
IL-only PE32 image with machine I386, the same header as an AnyCPU library.
Stage 113 also tests a copy of the x86 CoreLib with `IntPtr.Size` patched to 8.
Its header still says I386 PE32, and it's rejected.

The compiler checks the width lazily, the first time it lowers a body that the
CoreLib owns. Metadata-only use of a 64-bit CoreLib stays available: resolving
types, the System.Object root and its empty constructor, and signatures and
layouts. The WinForms startup probes rely on this to analyze the installed x64
pack.

Application and library assemblies, including AnyCPU and IL-only DLLs, aren't
checked. The compiler reads only IL, and ReadyToRun precompilation doesn't
change it. Comparing every method body by type and name between the installed
10.0.10 win-x86 and win-x64 packs gives this result:

- Microsoft.NETCore.App: only System.Private.CoreLib differs.
- Microsoft.WindowsDesktop.App: every WinForms assembly is identical. The
  differences are in WPF: the C++/CLI DirectWriteForwarder and System.Printing,
  and three PresentationFramework bodies.

Those WPF assemblies are outside the WinForms path and stay unchecked. Any
future need to compile them requires its own decision.

## Supported packs

| Pack | CoreLib header | `IntPtr.Size` IL | Result |
| --- | --- | --- | --- |
| Microsoft.NETCore.App 10.0.10 win-x86 | I386, PE32, ReadyToRun | 4 | lowered |
| Microsoft.NETCore.App.Runtime.Mono.browser-wasm 10.0.10 | I386, PE32, IL-only | 4 | lowered |
| Microsoft.NETCore.App 10.0.10 win-x64 | AMD64, PE32+, ReadyToRun | 8 | status -7 |

The host chooses the runtime pack. Identity matching doesn't authenticate the
supplied bytes (decision 0038). This check doesn't prove that every 32-bit
CoreLib body is supported. It prevents a 64-bit body from being silently
narrowed.

See [Stage 113](../../demos/stage-113-corelib-native-width/README.md).
