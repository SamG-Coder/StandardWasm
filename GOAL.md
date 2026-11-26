# Project goal

Run existing, compiled .NET applications in a browser, unmodified. A user drops a console, WinForms or Blazor application's published output (the `.exe` and its DLLs) into a page. The self-hosted StandardWasm compiler, running in that page, compiles the IL to WebAssembly (Wasm GC, tail calls and exception handling), and the application runs locally. Microsoft's own runtime and WinForms implementations are compiled as shipped. Only the Windows platform layer is replaced, with WebGPU as the graphics backend. See [decision 0024](docs/decisions/0024-binary-compatible-applications.md) and the [roadmap](docs/roadmap.md).

| Milestone | Acceptance evidence |
| --- | --- |
| Scalar IL | Library exports match the CLR for integers, branches, loops and calls. Deep tail recursion executes in a browser. See runnable demo stage 01. |
| Managed data and libraries | Add library fixtures for each implemented type or API, including boundary cases and failure behavior, compared against the CLR in Node and a real browser. |
| Self-hosting | A generated compiler reads its own DLL, emits a second compiler, and that compiler reproduces the module and passes the library suite. Track bootstrap dependencies individually. Required evidence: [decision 0004](docs/decisions/0004-self-hosting-acceptance.md). This is the compiler users run in the browser. |
| Full IL and upstream runtime | The self-hosted compiler compiles unmodified System.Private.CoreLib and the shared framework from dotnet/runtime, and the CLR oracle suite passes on top of them (roadmap steps C–F). |
| Console applications | An unmodified console `.exe` drop matches `dotnet App.dll` stdout, stderr and exit code (roadmap step G). |
| WinForms binary compatibility | Unmodified upstream dotnet/winforms runs on the browser platform layer: Win32 window manager, native common controls, and GDI/GDI+ on WebGPU (roadmap steps H–K). |
| Owner acceptance test | Build an ordinary .NET 10 WinForms application, including one using a third-party suite such as DevExpress. Drop the original compiled application and its DLLs into the browser, compile them with the self-hosted compiler, run them locally, and compare behavior, layout, events and drawing with the same application on Windows on the same device. No source changes, no Blazor dependency in the runtime, no remote Windows server, no application-specific UI replacements (roadmap step L). |
| Blazor applications | An unmodified Blazor WebAssembly application drop runs with Microsoft's Blazor libraries compiled by StandardWasm (roadmap step M). |

The custom WinForms-style controls in stages 14 onward are compiler and rendering demonstrations, not the compatibility architecture. Use the [demo index](demos/README.md) for runnable stages. Open GitHub issues track individual experiments and dependencies.
