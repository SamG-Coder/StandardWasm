# 0024 · Binary-compatible .NET applications in the browser

Status: proposed (#158). Direction set by the repository owner on 2026-11-26. It needs Codex's APPROVE because it changes the WinForms milestone that Codex's stages implement.

## Decision

StandardWasm's end goal is to run existing, compiled .NET applications in a browser. Applications are not rewritten, recompiled from source, or ported to a look-alike framework. A user drops the application's published output into a page. The self-hosted StandardWasm compiler, running in that page, compiles the managed code to WebAssembly, and the application runs locally.

The supported application models are:

| Model | Input | Platform layer replaced for the browser |
| --- | --- | --- |
| Console | `dotnet new console` style output | Console streams, file system, environment, clock |
| WinForms | .NET 10 WinForms application, including third-party control suites such as DevExpress | Win32 windowing and messages, the native common controls, GDI, GDI+, COM/OLE, theming, IME, UI Automation |
| Blazor | Blazor WebAssembly and Blazor Hybrid style applications | The JS interop and renderer host. Microsoft's Blazor libraries themselves are compiled, not replaced. |

Blazor Server needs a server process and is out of scope. StandardWasm's own compiler and runtime stay independent of Blazor. Blazor is one application model the runtime can execute, not a dependency.

### What gets compiled and what gets replaced

Microsoft's open-source implementations are the foundation:

- [dotnet/runtime](https://github.com/dotnet/runtime): System.Private.CoreLib and the rest of the shared framework.
- [dotnet/winforms](https://github.com/dotnet/winforms): System.Windows.Forms, System.Windows.Forms.Primitives, System.Private.Windows.Core, System.Drawing.Common, System.Private.Windows.GdiPlus and the design-time assemblies applications load.
- [dotnet/aspnetcore](https://github.com/dotnet/aspnetcore): Microsoft.AspNetCore.Components and the WebAssembly host, for Blazor.

Their managed code is compiled as shipped, from the same compiled assemblies an application references. It is not forked. Only the boundary where managed code leaves for the operating system is replaced: P/Invoke into user32, gdi32, comctl32, gdiplus, ole32, uxtheme, imm32, kernel32 and the runtime's native shims. The replacement is a browser platform layer:

- **Windowing and input.** Win32 window manager semantics (HWND tree, message queue, `SendMessage`/`PostMessage`, focus, capture, z-order, DPI) run on the browser event loop and DOM pointer, keyboard, IME and clipboard events.
- **Native controls.** The standard WinForms controls are wrappers. TextBox, ListView, TreeView, ComboBox, tooltips, tabs and the month calendar render and edit through native user32/comctl32 window classes driven by `EM_*`, `LVM_*`, `TVM_*` and similar messages (see the [audit](../winforms-dependency-audit.md)). The platform layer must provide those window classes with their message contracts.
- **Drawing.** GDI (`HDC`, `BitBlt`, `SelectObject`, regions, fonts) and the GDI+ flat API (517 `Gdip*` entry points used by System.Drawing.Common) are implemented on WebGPU. WebGPU is the graphics backend under WinForms, not a substitute for the WinForms control framework.
- **COM/OLE, theming, accessibility.** Implemented as far as applications observe them: drag and drop, clipboard, visual styles metrics, UI Automation mapped to ARIA.

Where the platform layer can be written in C#, it is compiled by StandardWasm like everything else, and its behavior is compared against real Windows on the same inputs.

### Accepted inputs

- **Framework-dependent publish** (the default): `App.exe` is the native apphost, and the managed entry point is `App.dll` beside `App.deps.json` and `App.runtimeconfig.json`. The loader reads the apphost only to find those files.
- **Single-file publish:** the managed assemblies are bundled inside `App.exe`, and the loader extracts them from the bundle.
- **Self-contained publish:** the framework assemblies come from the drop instead of the browser-side framework cache.
- **ReadyToRun assemblies:** the native code is ignored and the IL is compiled.
- **NativeAOT executables and mixed-mode C++/CLI assemblies** contain no compilable IL for some or all code. They are rejected with an explicit diagnostic.

### Acceptance test (owner-defined)

1. Build an ordinary .NET 10 WinForms application, including one that uses a commercial third-party suite such as DevExpress (no particular version).
2. Take the original compiled application and its dependency DLLs, without changing application logic.
3. Drag and drop them into the browser.
4. Compile them with the self-hosted StandardWasm compiler, in the page.
5. Execute and render the application locally in the browser.
6. Compare behavior, layout, events and drawing against the original running on Windows, on the same device.

No application source changes, no Blazor dependency in the runtime, no remote Windows server, and no application-specific UI replacements. Console and Blazor applications follow the same steps with their own comparisons: stdout/stderr and exit code, and the rendered DOM plus interaction transcript.

Third-party binaries such as DevExpress are licensed. They are never committed. Acceptance runs use a locally installed, licensed copy, and the repository keeps only the test application source and the comparison harness.

## Consequences

- **Compiler scope** grows from the .NET Standard subset to full ECMA-335 as emitted for `net10.0`:
  - generics with shared and specialized instantiation
  - virtual and interface dispatch, including default interface methods
  - static constructors
  - reflection over metadata
  - unsafe code and function pointers
  - `Span<T>` and `stackalloc`
  - finalizers and `SafeHandle`
  - threading primitives
  - P/Invoke and `ComWrappers` redirection into the platform layer

  The compiler still reads the compiled DLL, never source.
- **Memory model must be decided by experiment before unsafe code lands.** Upstream WinForms uses `unsafe` in 419 of 2,233 source files, plus `fixed`, pointers into managed arrays and structs, and `delegate*`. Wasm GC objects are not addressable by pointer. Candidate designs are:
  - GC objects plus copy-in/copy-out pinning
  - a linear-memory heap with a custom collector
  - a hybrid split by type

  The choice gets its own decision, backed by a measured fixture, before the compiler accepts pointer IL on managed memory.
- **Threading.** The browser main thread cannot block. `Application.Run`, `Control.Invoke`, `SynchronizationContext` and modal loops (`ShowDialog`, `MessageBox`) must be mapped onto the browser event loop. Possible approaches are cooperative re-entry, JS Promise Integration, or Wasm threads where cross-origin isolation allows. This is also an experiment-first decision.
- **Self-hosting matters more.** Compilation happens in the user's browser, so decision 0004's second-generation compiler is the compiler users run. In-browser compile time and caching of compiled framework assemblies (IndexedDB, keyed by assembly MVID and compiler hash) become measured requirements.
- **Existing demonstration controls stay as demonstrations.** Stages 14 onward (the custom counter, textbox, control tree and profile demos) remain valid compiler and rendering tests. They are not the compatibility architecture, and new work should not extend them as a substitute for upstream WinForms.
- **Existing infrastructure is kept.** The portable compiler, the CLR oracle, Node/Edge comparison, decision 0004's self-hosting checks and the review process all continue. Each roadmap step adds library fixtures compared against the CLR, as before.
- **Identity rules carry over.** Platform types and members are recognized only through culture-neutral platform AssemblyRefs (#144, #141). With whole frameworks compiled, the platform layer is also matched by exact assembly identity, never by name.

The staged plan is in [docs/roadmap.md](../roadmap.md), and the dependency audit is in [docs/winforms-dependency-audit.md](../winforms-dependency-audit.md).
