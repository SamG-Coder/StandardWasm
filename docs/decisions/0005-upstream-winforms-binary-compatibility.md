# 0005 · Upstream WinForms binary compatibility

**Status:** Accepted direction. Implementation and compatibility evidence are pending.

## Decision

Use the Microsoft WinForms and .NET runtime source trees as the foundation for browser compatibility with existing compiled .NET 10 applications, including WinForms, console and Blazor apps. Reuse managed control, layout, painting, component, event, accessibility and runtime logic where it can run unchanged. Maintain traceable patches for browser-specific behavior, with each change tied to an upstream dependency and a parity test. Preserve upstream repository and commit provenance and track the corresponding licenses and notices.

StandardWasm remains the compiler of record. Continue developing the metadata reader, assembly linker, IL lowering, Wasm emitter, diagnostics and self-hosted compiler. Use upstream .NET runtime and library implementations where possible and validate shared semantics against Windows .NET. The upstream Mono browser host is a source of reusable components and reference behavior; accepting an application compiled by a different toolchain does not satisfy the self-hosted compilation gate.

Separate the following boundaries:

| Boundary | Responsibility |
| --- | --- |
| Browser input and assembly binding | Load the unmodified managed application and dependencies, dependency manifests, satellite DLLs and resources. Preserve assembly identity, type identity and version policy. Resolve known desktop framework references to an explicitly selected browser build of the corresponding upstream framework. Validate identities and handle missing or incompatible imports explicitly. |
| Compiler and managed runtime | Emit Wasm for the managed code actually executed. Supply the CLR object model, GC and lifetime, generic instantiation, metadata/reflection, exceptions, initialization, delegates, runtime intrinsics and platform invocation semantics, using appropriate WebAssembly features and upstream .NET code where possible. Track any path that needs dynamic loading or generated code; demonstrate how the self-hosted compiler handles it before counting that path as compatible. |
| Upstream WinForms and drawing | Retain the real `System.Windows.Forms` implementations and public API contracts. Keep framework identity distinct from the compiler's demo namespaces. Adapt the managed/native boundary behind `System.Drawing`, GDI/GDI+ and Win32 services with coherent shared semantics. |
| Browser platform host | Supply an event pump, virtual window/handle state, input, timers, focus, DPI, accessibility, and browser-mediated services as needed. Convert drawing commands to WebGPU with a backend that preserves clipping, transforms, text measurements, compositing and resource lifetimes. DOM facilities can support input, accessibility or permissions without taking over the managed control model. |

The browser launcher accepts a Windows executable as the front door, discovers its original managed code and companions, and compiles the managed entry point. For single-file deployments it reads the original bundle in the browser. The Windows native apphost is launch metadata. Windows-native code without equivalent managed IL requires a separate port or platform implementation. See [Microsoft’s publishing model](https://learn.microsoft.com/dotnet/core/deploying/).

Console applications use the same loader and compiler with a local terminal host, including arguments, streams and exit status. Blazor is an input kind with its own upstream component renderer and asset conventions. Begin with published client applications. Server-dependent render modes need separate compatibility for their hosting and services, as described in the [ASP.NET Core render mode documentation](https://learn.microsoft.com/aspnet/core/blazor/components/render-modes?view=aspnetcore-10.0). The generic launcher and other app hosts do not depend on Blazor.

“Original DLLs” applies to application and third-party binaries. The browser framework and runtime are port builds. Keep trusted framework binding explicit and reproducible; do not strip, globally bypass, or rewrite the identity of application and vendor assemblies. A first browser milestone must prove these bindings with an existing Windows build of a real WinForms application.

## Consequences

- A browser host and documented platform imports are legitimate for desktop applications. Import-free .NET Standard demos and the self-hosted compiler continue to serve as independent verification.
- Browser event delivery, synchronous window messages, modal state, text shaping and native-control state require observable compatibility tests. A visual substitute for a control does not count as running that control.
- Direct Windows-native dependencies require a shared browser port or a verified platform adapter. Surface unavailable behavior with diagnostics that identify the assembly, method or platform operation. Do not count a silently stubbed operation as supported.
- Keep existing demo controls and benchmarks as infrastructure. Prioritise upstream framework blockers and unchanged-binary parity over further performance work on demo controls.
- Record the DevExpress version used by the fixture without setting a project-wide version requirement. Use locally supplied licensed third-party binary artifacts and keep them outside the public repository.
- Preserve the [self-hosting verification](0004-self-hosting-acceptance.md) and [independent PR review](../../AGENTS.md). Track implementation and acceptance gates in the [dependency audit and roadmap](../upstream-winforms-roadmap.md).
