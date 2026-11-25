# Project goal

Compile .NET Standard library assemblies into standalone browser WebAssembly with a custom IL compiler. Demonstrate enough managed runtime support to compile the compiler itself, then support WinForms applications with WebGPU drawing.

| Milestone | Acceptance evidence |
| --- | --- |
| Scalar IL | Library exports match the CLR for integers, branches, loops and calls. Deep tail recursion executes in a browser. See runnable demo stage 01. |
| Managed data and libraries | Add library fixtures for each implemented type or API, including boundary cases and failure behavior. Stage 02 establishes the first managed array case. Strings, objects and linking are open work. |
| Self-hosting | A generated compiler reads its own DLL, emits a second compiler, and that compiler reproduces the module and passes the library suite. Track bootstrap dependencies individually. |
| WinForms and WebGPU | Run the same form source on Windows and in a browser, verify drawing and input, and measure graphics costs on the same device against a documented baseline. |

Use the [demo index](demos/README.md) for runnable stages. Open GitHub issues track individual experiments and dependencies.
