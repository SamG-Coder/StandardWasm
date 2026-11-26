# Project goal

Run existing compiled .NET 10 applications locally in a browser, including Windows Forms, console and Blazor applications. StandardWasm is the custom ahead-of-time compiler. Use Microsoft's [WinForms](https://github.com/dotnet/winforms) and [.NET runtime](https://github.com/dotnet/runtime) implementations as the managed foundation. Preserve upstream managed implementations wherever technically possible and adapt the Windows platform dependencies. Use WebGPU as the graphics backend.

## Acceptance

1. Build an ordinary .NET 10 WinForms application using DevExpress. Record the exact vendor packages and prove the application runs on Windows. The DevExpress version is a fixture detail.
2. Record hashes for the application's original executable, managed entry assembly, dependencies, manifests and resources. Drop the original Windows executable into the browser. For an apphost with separate files, drop its original publish folder or supply the missing companions. An ordinary supported single-file .NET executable should launch from one drop. The application and vendor code require no porting or rewriting.
3. Compile the managed application and its required dependencies locally in the browser with the self-hosted StandardWasm compiler, resolving framework references against the upstream-based browser framework. Run the emitted WebAssembly locally with a browser platform host.
4. Compare against the original Windows run using the same inputs. Verify behavior, event order, layout, text and drawing, including the actual DevExpress controls. Record pixel tolerances, fonts, DPI and known platform differences; measure rendering costs on the same device.

The WinForms and console paths require no Blazor. The acceptance paths require no remote Windows execution, application source changes, or application-specific UI replacements. The shipped browser host supplies platform services. Custom controls in the existing demos are compiler and renderer fixtures.

| Milestone | Acceptance evidence |
| --- | --- |
| Compiler semantics | Ordinary compiled .NET Standard fixtures agree with the CLR in Node and a real browser. Keep deterministic lowering, explicit diagnostics and staged tests as support grows toward .NET 10 desktop assemblies. |
| Self-hosting | A generated compiler reads its own DLL, emits a second compiler, and that compiler reproduces the module and passes the library suite. Follow [decision 0004](docs/decisions/0004-self-hosting-acceptance.md). |
| Upstream framework | Launch an unchanged .NET 10 WinForms DLL in the browser and verify the actual upstream control's behavior against Windows. Grow compatibility through general platform adapters. |
| Third-party binary parity | Pass the DevExpress acceptance test above with the original executable and unchanged managed dependencies. |
| Console launch | Drop an ordinary .NET console publish into the browser and compare entry arguments, input/output, errors and exit status with the same program on the CLR. |
| Blazor launch | Drop an existing compiled Blazor app and its published assets. Start with a standalone client app and compare rendering and interaction with its stock host. Track server-dependent render modes as separate hosting work. |

Read the [architecture decision](docs/decisions/0005-upstream-winforms-binary-compatibility.md), the [dependency audit and roadmap](docs/upstream-winforms-roadmap.md), and the [demo index](demos/README.md). Use the existing [independent review process](AGENTS.md).
