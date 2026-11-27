# Upstream WinForms dependency audit and roadmap

This is a source audit and implementation sequence. It does not report a successful build of upstream WinForms for the browser. The acceptance target is [unchanged WinForms application binaries](../GOAL.md), under [decision 0024](decisions/0024-upstream-winforms-binary-compatibility.md).

## Source baseline

The audit used .NET 10 release-branch snapshots. Pin the actual matching runtime, Windows Desktop framework, SDK and source revisions when building the first reference fixture. These audit snapshots record what was inspected and are not a claim that the two commits are a release pair.

| Upstream | Audited source |
| --- | --- |
| WinForms | [067fc6232013d77f7fded3c3c6797f0800ff40c4](https://github.com/dotnet/winforms/commit/067fc6232013d77f7fded3c3c6797f0800ff40c4) |
| .NET runtime | [a84d826750643e4e849c510a443f9a86f3752a4d](https://github.com/dotnet/runtime/commit/a84d826750643e4e849c510a443f9a86f3752a4d) |

## Dependency inventory

| Area | Upstream evidence and present boundary | Proposed browser seam and first probe |
| --- | --- | --- |
| Framework graph | The [WinForms project](https://github.com/dotnet/winforms/blob/067fc6232013d77f7fded3c3c6797f0800ff40c4/src/System.Windows.Forms/System.Windows.Forms.csproj) references Accessibility, `System.Private.Windows.Core` and `System.Windows.Forms.Primitives`, with dependencies on `System.Configuration.ConfigurationManager`, `System.Resources.Extensions` and `System.Windows.Extensions`. It uses private source generators during the framework build. | Capture the selected Windows Desktop framework graph and port builds by assembly identity, including generated interop code, transitive references, resource and satellite assemblies. Bootstrap `ApplicationConfiguration.Initialize` and `Application.Run` from an ordinary application DLL. |
| Runtime and libraries | [.NET CoreLib shared code](https://github.com/dotnet/runtime/tree/a84d826750643e4e849c510a443f9a86f3752a4d/src/libraries/System.Private.CoreLib) is distinct from runtime contracts such as [Mono CoreLib](https://github.com/dotnet/runtime/tree/a84d826750643e4e849c510a443f9a86f3752a4d/src/mono/System.Private.CoreLib). Existing browser code handles [assets](https://github.com/dotnet/runtime/blob/a84d826750643e4e849c510a443f9a86f3752a4d/src/mono/browser/runtime/assets.ts), [GC handles](https://github.com/dotnet/runtime/blob/a84d826750643e4e849c510a443f9a86f3752a4d/src/mono/browser/runtime/gc-handles.ts), [ICU](https://github.com/dotnet/runtime/blob/a84d826750643e4e849c510a443f9a86f3752a4d/src/mono/browser/runtime/icu.ts) and [startup](https://github.com/dotnet/runtime/blob/a84d826750643e4e849c510a443f9a86f3752a4d/src/mono/browser/runtime/startup.ts). | Inventory which managed libraries, internal calls and browser tools can be reused and define the runtime contract of StandardWasm-compiled code. Check initialization, object layout, virtual dispatch, generic metadata, reflection, typed exceptions, resources, globalization and interop with executable probes. Audit dynamic loading and code creation separately. |
| Windowing and dispatch | [ComponentThreadContext](https://github.com/dotnet/winforms/blob/067fc6232013d77f7fded3c3c6797f0800ff40c4/src/System.Windows.Forms/System/Windows/Forms/Application.ComponentThreadContext.cs) uses native message loops and COM registration. [NativeWindow](https://github.com/dotnet/winforms/blob/067fc6232013d77f7fded3c3c6797f0800ff40c4/src/System.Windows.Forms/System/Windows/Forms/NativeWindow.cs) creates and subclasses HWNDs, resolves a User32 window procedure and uses native window messages. [WindowsFormsSynchronizationContext](https://github.com/dotnet/winforms/blob/067fc6232013d77f7fded3c3c6797f0800ff40c4/src/System.Windows.Forms/System/Windows/Forms/WindowsFormsSynchronizationContext.cs) is part of UI-thread dispatch. | Provide browser-owned handle state and map messages, invalidation, timer and posted work onto the browser event loop. Preserve synchronous dispatch, lifecycle order, modal behavior and reentrancy where needed. Compare load/show/paint, click, timer, `BeginInvoke`, focus and close traces. |
| Native control behavior | [Control](https://github.com/dotnet/winforms/blob/067fc6232013d77f7fded3c3c6797f0800ff40c4/src/System.Windows.Forms/System/Windows/Forms/Control.cs) and [Button](https://github.com/dotnet/winforms/blob/067fc6232013d77f7fded3c3c6797f0800ff40c4/src/System.Windows.Forms/System/Windows/Forms/Controls/Buttons/Button.cs) combine managed behavior with native handles and control messages. Windows Common Controls and theme services need a reachability audit. | Reuse managed controls and implement shared message-level behavior where native widget implementations supply semantics. Test keyboard navigation, editing, selection, scrolling, capture, owner draw, DPI and layout; log reached Win32 operations so missing behavior cannot pass silently. |
| Drawing and text | The WinForms source tree contains [System.Drawing.Common](https://github.com/dotnet/winforms/blob/067fc6232013d77f7fded3c3c6797f0800ff40c4/src/System.Drawing.Common/src/System.Drawing.Common.csproj) and [Windows GDI+ bindings](https://github.com/dotnet/winforms/blob/067fc6232013d77f7fded3c3c6797f0800ff40c4/src/System.Private.Windows.GdiPlus/System.Private.Windows.GdiPlus.csproj). [Graphics](https://github.com/dotnet/winforms/blob/067fc6232013d77f7fded3c3c6797f0800ff40c4/src/System.Drawing.Common/src/System/Drawing/Graphics.cs) creates graphics from HWNDs and HDCs using GDI+. [TextRenderer](https://github.com/dotnet/winforms/blob/067fc6232013d77f7fded3c3c6797f0800ff40c4/src/System.Windows.Forms/System/Windows/Forms/Rendering/TextRenderer.cs) uses GDI device contexts and fonts. [Microsoft documents System.Drawing.Common's Windows-only baseline](https://learn.microsoft.com/dotnet/core/compatibility/core-libraries/6.0/system-drawing-common-windows-only). | Keep the drawing API and native-handle semantics behind a WebGPU backend. Probe fills, paths, bitmaps, clipping, transforms, text metrics and drawing, color and lifetime semantics. Treat text measurement and font fallback as separate gates from drawing pixels. |
| Accessibility and platform services | [AccessibleObject](https://github.com/dotnet/winforms/blob/067fc6232013d77f7fded3c3c6797f0800ff40c4/src/System.Windows.Forms/System/Windows/Forms/Accessibility/AccessibleObject.cs) exposes UI Automation provider interfaces. [Clipboard](https://github.com/dotnet/winforms/blob/067fc6232013d77f7fded3c3c6797f0800ff40c4/src/System.Windows.Forms/System/Windows/Forms/OLE/Clipboard.cs) and [DataObject](https://github.com/dotnet/winforms/blob/067fc6232013d77f7fded3c3c6797f0800ff40c4/src/System.Windows.Forms/System/Windows/Forms/OLE/DataObject.cs) sit at the OLE boundary. [TaskDialog](https://github.com/dotnet/winforms/blob/067fc6232013d77f7fded3c3c6797f0800ff40c4/src/System.Windows.Forms/System/Windows/Forms/Dialogs/TaskDialog/TaskDialog.cs), [SystemInformation](https://github.com/dotnet/winforms/blob/067fc6232013d77f7fded3c3c6797f0800ff40c4/src/System.Windows.Forms/System/Windows/Forms/SystemInformation.cs) and [PrintDocument](https://github.com/dotnet/winforms/blob/067fc6232013d77f7fded3c3c6797f0800ff40c4/src/System.Drawing.Common/src/System/Drawing/Printing/PrintDocument.cs) are additional Windows-facing surfaces. | Project the real accessibility tree and focus into browser accessibility and translate supported clipboard, drag/drop, file and print operations under browser permissions. Audit actual activation of COM, P/Invoke, registry, process, filesystem and other desktop-only services per fixture. Keep unsupported behavior visible. |
| Third-party libraries | No DevExpress assembly was supplied for this source audit. Its transitive managed and native calls, embedded resources, feature/license initialization and dynamically loaded dependencies must be measured from an actual Windows fixture. | Start with actual controls and designer resources, then measure grid/editor behavior, styling and custom drawing. Inventory and test the actual calls before adding general adapters. No per-application control replicas. |

The first implementation issue should produce a machine-readable inventory from the Windows reference fixture. Identify the Windows apphost and its companion files or bundle. Walk managed assembly references and type forwards, P/Invoke declarations, resource containers and known dynamic loads. Record observed entry-point, binding and platform-service traces separately from static reachability, with assembly, member and diagnostic location. Classify each reached dependency as upstream reusable, browser port or unresolved. Record ownership and validation for each port. Use the [upstream managed patch rule](decisions/0024-upstream-winforms-binary-compatibility.md#decision) for exceptions at the native import boundary. Verify DevExpress compatibility with its actual binaries.

## Staged implementation

| Gate | Implementation | Done when |
| --- | --- | --- |
| W0 · Windows reference | Create and build an ordinary `net10.0-windows` WinForms fixture. Start with upstream controls and create a separate commercial control fixture, starting with DevExpress when licensed dependencies are available. Record the precise package versions. | The representative apps run on Windows. Record SDK, desktop runtime, fonts, DPI, assembly and resource hashes, scripted interactions and expected results. |
| W1 · Desktop assembly loading | Extend the linker and compiler using actual framework inputs. Classify managed PE, Windows apphost with companions and single-file .NET bundle. Add deterministic identity/version binding, transitive compilation, startup and resource tests. | An unchanged executable drop selects the original managed entry assembly and resolves its framework graph for the generated compiler. Missing companions, unsupported native inputs and unsupported IL produce explicit diagnostics. |
| W2 · Upstream window and event pump | Compile and run an ordinary upstream Form and controls with a browser host for virtual handles, messages and scheduling that follows [decision 0026](decisions/0026-modal-message-loops.md). | Windows/Edge traces agree on lifecycle, event order, focus, timer, cross-thread UI posting where exercised and disposal. Upstream managed controls own the application state. |
| W3 · WebGPU drawing and resource parity | Implement drawing, metrics and invalidation behind upstream platform seams. Add real resx/satellite resources, common control messages, input and accessibility probes. | Windows and browser agree on layout and controlled text metrics, with reproducible images and documented pixel tolerances. Same-device measurements separate paint, submission and completion time. |
| W4 · Third-party compatibility | Load a commercial control fixture, starting with DevExpress. Derive reached dependencies and expand the general compiler, runtime and platform support. | Its unchanged vendor control code loads, lays out, paints and handles representative interaction in Edge. Every required native dependency is reproduced or has a compatible shared browser implementation. |
| W5 · End-to-end acceptance | Drag and drop the original Windows executable and its original publish output into Edge. Verify one-file launch separately with an ordinary single-file publish. Supply only project-owned upstream-based framework/host assets in addition to those outputs. Compile locally with the self-hosted compiler and compare the run with Windows. | Hashes show the application and vendor DLLs are unchanged. There is no remote execution or app-specific port. Automated traces and visual reports cover the [GOAL.md acceptance test](../GOAL.md). |

The [Windows reference fixture](../demos/stage-72-winforms-windows-reference/README.md) captures the stock-control baseline and static dependency inventory. The [executable intake fixture](../demos/stage-73-executable-intake/README.md) recovers the original managed entry from a supported apphost publish or single-file bundle. The [entry execution fixture](../demos/stage-74-executable-entry/README.md) runs a parameterless console entry in Edge and matches Windows exit status for both publish formats. The [managed companions fixture](../demos/stage-95-managed-companions/README.md) reads the original publish manifest, compiles its managed companions, and reproduces exit code 61 in Windows, Node and Edge. It sends the original WinForms entry and installed framework assemblies through the same browser worker. WinForms stops at a compiler diagnostic before creating a window. The [blocker census](../demos/stage-117-winforms-census/README.md) walks the same entry's static call graph through `Application.Run` and records the portable compiler's verdict for every reached method. It ranks the remaining compiler features and the reached Win32 import surface.

### Managed finalization

Both unchanged WinForms launch forms currently reach CoreLib's `GC.SuppressFinalize`
while the compiler resolves the startup closure. CoreLib validates the argument,
checks the concrete runtime type for a finalizer, and delegates suppression to the
collector. The reference framework includes finalizer declarations in
`System.ComponentModel.Component`, WinForms and System.Drawing.Common. Inherited
finalizers matter when the concrete type comes from an ordinary application DLL.
See the [.NET 10 CoreLib implementation](https://github.com/dotnet/runtime/blob/v10.0.10/src/coreclr/System.Private.CoreLib/src/System/GC.CoreCLR.cs).

The startup census for the .NET 10.0.10 reference finds a call chain from
`Program.Main` through `Application.Run` and the `ApplicationContext`
constructor to `GC.SuppressFinalize`. The constructor suppresses finalization
for the exact stock application context and stock modal context types. Its
finalizer calls virtual `Dispose(false)` on derived instances. A Windows
CLR probe with two derived instances observed one explicit disposal and
one finalizer disposal on both x86 and x64. Preserve that distinction
when adding the browser runtime path.

Before claiming finalization support, compare an ordinary library on the CLR,
Node and Edge. Cover the null argument exception, repeated suppression, two
instances of the same type, inherited finalizers, re-registration and
resurrection. A collector design needs to keep the finalizable object and its
fields available to its managed finalizer after the last ordinary root is gone.
Evaluate explicit managed root tracing and a finalization queue for that contract.
JavaScript cleanup notifications can release independent host resources, but
notification timing and delivery alone cannot satisfy the managed queue and
resurrection tests. See the [ECMAScript memory management specification](https://tc39.es/ecma262/multipage/managing-memory.html#sec-finalization-registry-objects)
and the [.NET Wasm GC design discussion](https://github.com/dotnet/runtime/issues/94420).

A Node and Edge probe observed that a cleanup registration holding a strong
Wasm reference kept its target alive. Once that reference was cleared, cleanup
ran after the target was gone. A compatible collector must instead queue the
managed object while its state is available. In the upstream single-threaded
Wasm runtime, a browser background job schedules the drain of managed GC work.
See the [.NET 10 Mono GC implementation](https://github.com/dotnet/runtime/blob/v10.0.10/src/mono/mono/metadata/gc.c).

## Additional application hosts

Share W1 assembly loading and the compiled entry-point path. Keep app-specific services behind host boundaries.

| Track | First acceptance gate | Remaining scope |
| --- | --- | --- |
| Console | Run an ordinary compiled app with command arguments, interactive standard input, standard output and error, and a meaningful exit status. Compare the same binary and interaction transcript with the CLR. | Browser scheduling and filesystem, network and terminal semantics. Detect programs that require unsupported OS services. |
| Blazor | Drop an ordinary standalone Blazor WebAssembly publish and its static assets. Use the managed IL with StandardWasm and preserve the upstream component renderer. Compare navigation, event handling, component state and rendering with the stock host. Existing generated Wasm alone does not count as self-hosted compilation. | Static/server/auto render modes and applications that require server-side services need a separate host plan and dependency probes. Keep those cases visible rather than relabeling them as client execution. |

Work in separate issues and worktrees. Keep the compiler/bootstrap regression suite and use focused upstream fixtures to bridge each remaining gap. Gates may be developed in parallel when their interfaces are independent; passing a later smoke test does not waive an earlier acceptance condition.
