# StandardWasm

StandardWasm is an experimental ahead-of-time compiler for .NET Standard assemblies. Its goal is to turn managed library IL into standalone WebAssembly modules that a browser can instantiate directly. C# is the first source language. The compiler boundary is the compiled DLL.

## Milestones

1. Compile ordinary scalar library methods, including branches, loops and calls. Compare exported results against the same DLL running on .NET.
2. Extend the supported IL and library surface using small, independently buildable .NET Standard fixtures. Use Wasm GC for managed storage and tail calls where the managed semantics permit them. Validate the actual binaries in a browser.
3. Bring up the compiler's own dependencies. Self-hosting passes when a generated Wasm compiler reads its own managed assembly, produces a second Wasm compiler, and that compiler reproduces the module and passes the library tests.
4. Add a browser backend for WinForms and use WebGPU for drawing. Measure rendering cost against a baseline on the same device.

.NET Standard specifies an API contract. Each supported API needs either a compatible implementation compiled into the module or an explicitly documented host import. Windows services and UI controls require additional browser implementations. Blazor is outside this project's architecture.

## Engineering approach

Separate assembly loading, IL lowering, Wasm emission, library implementations and browser integration. Keep compiler-specific decisions in the compiler and document the mapping wherever CLR and Wasm behavior differ. Grow support one executable library fixture at a time. Use deterministic test inputs and report unsupported IL with its method and offset.

## Status

Project bootstrap. Full .NET Standard coverage, self-hosting, WinForms and WebGPU rendering are milestones. See the commit history for implemented compiler paths.
