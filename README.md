# StandardWasm

StandardWasm is an experimental ahead-of-time compiler for .NET Standard assemblies. Its goal is to turn managed library IL into standalone WebAssembly modules that a browser can instantiate directly. C# is the first source language. The compiler boundary is the compiled DLL.

## Start here

- [Project goal, milestones and acceptance criteria](GOAL.md)
- [Runnable demos by stage](demos/README.md)
- [WinForms and WebGPU counter demo](demos/stage-14-winforms-webgpu/README.md)
- [Shared agent workflow](AGENTS.md) and [Claude entry point](CLAUDE.md)
- [Architecture decisions](docs/decisions/0001-staged-demos.md)

.NET Standard specifies an API contract. Each supported API needs either a compatible implementation compiled into the module or an explicitly documented host import. Windows services and UI controls require additional browser implementations. Blazor is outside this project's architecture.

## Engineering approach

Separate assembly loading, IL lowering, Wasm emission, library implementations and browser integration. Keep compiler-specific decisions in the compiler and document the mapping wherever CLR and Wasm behavior differ. Grow support one executable library fixture at a time. Use deterministic test inputs and report unsupported IL with its method and offset.

## Status

The first working backend takes a DLL and a public export type, follows reachable in-assembly static calls, and emits a standalone module with zero runtime imports. Public static methods become named exports. The host compiler runs on .NET 10. Mono.Cecil reads the IL, our C# backend lowers it to WAT, and Binaryen assembles the binary.

| Available today | Current boundary |
| --- | --- |
| 32-bit integers, unsigned comparisons, booleans, locals, loops, conditional branches, static calls and recursion | No general BCL linker, virtual dispatch, generics, strings or floating-point lowering |
| Managed one-dimensional int and byte arrays backed by Wasm GC | Allocation, length, indexed read and write for int and packed byte elements |
| Direct tail calls for eligible call/return pairs | Managed exception handling and stack-trace fidelity are open |
| Integer arithmetic and array access | The portable compiler uses typed tags for checked overflow and integer divide-by-zero. Stages 50 and 51 support ordered arithmetic catches and preserve the caught Wasm exception across C# rethrow. Stages 52 and 53 add typed bounds and null exceptions to supported array, instance field and string operations. Stage 55 adds typed overflow for negative allocation lengths and typed null failures for supported delegate invocations. Stage 54 runs nested finally blocks on normal exits and supported tagged exceptions. Stage 56 preserves typed null failures for known instance calls and event-field compare-exchange. Stage 57 links scalar exception rethrow and cleanup across .NET Standard assembly boundaries. Stage 58 passes primitive arrays and strings through linked static calls. Stage 59 keeps class layouts separate by assembly and passes opaque object references across DLLs. Stage 60 links static signatures that name public classes by assembly identity. Exception objects, native traps within finally regions, and native-sized checked casts are open. |

## Build and test

Install the .NET 10 SDK and Node 24 or newer. From this directory run:

```sh
npm ci
npm test
```

The harness uses the installed Microsoft Edge on Windows. On another platform, set `BROWSER_PATH` to a Chromium executable or install the matching Playwright browser with `npx playwright-core install chromium`. The test builds the netstandard2.0 demo libraries, records expected results on the CLR, compiles the DLLs and executes the binaries in Node and a headless browser. It also verifies rejection of checked addition and exercises one million tail calls. Traps are compared as failures, without matching exception classes.

To compile another DLL:

```sh
npm run compile -- path/to/Library.dll YourNamespace.ExportType artifacts/library.wasm
```

After running the tests, serve the repository locally, for example with `python -m http.server 8000 --bind 127.0.0.1`. Open localhost:8000 in a recent browser and run this example in its developer console:

```js
const { instance } = await WebAssembly.instantiateStreaming(fetch("./artifacts/Sample.wasm"));
instance.exports.SumSquares(10); // 385
```

## Self-recompilation

Stage 13 generates a compiler with named compilation and buffer exports. The generated compiler recompiles its own DLL using its own buffer APIs and reproduces the module byte for byte. The second generation reruns the portable generated-code fixtures in Node and Edge. Running npm test writes the Node-verified final generation to artifacts/StandardWasm.Compiler.wasm. See the [stage instructions](demos/stage-13-complete-bootstrap/README.md). Stage 23 adds [portable assembly bundle linking](demos/stage-23-assembly-linking/README.md). Stage 24 adds [portable 64-bit scalar integers](demos/stage-24-portable-i64/README.md), including linked calls and an exact BigInt boundary.

## Source map and bootstrap dependencies

- `Compiler/ModuleCompiler.cs` selects exports, links reachable methods and maps CLR types.
- `Compiler/MethodCompiler.cs` translates IL evaluation stacks and control flow. Basic blocks currently use a dispatcher for correctness. Efficient structured control flow is a future optimization.
- `Compiler/IntegerOps.cs` maps numeric and comparison instructions.
- `demos/stage-*/` contains the independently buildable .NET Standard libraries. `Verify/` generates deterministic expected results on the CLR.
- `src/AssemblyReader/` contains the portable PE/CLI discovery code shared by the reader stages.
- `src/PortableCompiler/` contains the compiler-in-Wasm path demonstrated by Stages 09 through 13.
- `scripts/` contains assembly, engine validation and browser tests. Generated modules are written to `artifacts/`.

The original CLI uses Mono.Cecil to read IL and Binaryen to assemble WAT. It bootstraps the portable compiler, which has a source-level PE/CLI reader and binary writer. Portable output modules have zero runtime imports. Expanding input compatibility and building the WinForms/WebGPU backend are separate milestones.

The modern instructions used here are standardized Wasm GC array instructions and `return_call`. See the [WebAssembly 3.0 release](https://webassembly.org/news/2025-09-17-wasm-3.0/) and [engine feature matrix](https://webassembly.org/features/).
