# Stage 117 · WinForms blocker census

Run `npm run demo -- stage-117-winforms-census` on Windows x64 with the
.NET 10 SDK and the win-x86 Microsoft.NETCore.App and
Microsoft.WindowsDesktop.App 10.0.10 runtimes. [Decision 0044](../../docs/decisions/0044-corelib-native-width.md)
lowers CoreLib bodies only from a 4-byte native-width CoreLib, so the census
reads the win-x86 packs. Set `STANDARDWASM_X86_DOTNET` to the win-x86 host if
it isn't in the default location. Set `STANDARDWASM_DESKTOP_VERSION` to census
another installed version, and `STANDARDWASM_CENSUS_PARALLEL` to change the
number of concurrent compiler verdicts (default 12). A run takes about 3.5
minutes. `STANDARDWASM_CENSUS_REUSE=<census.json>` replays an earlier run's
verdicts so the rankings can be regenerated without recompiling.

The runner publishes the unchanged [Stage 72](../stage-72-winforms-windows-reference/README.md)
reference application. A host-side analyzer then walks the static call graph
from its `Program.Main` through `Application.Run` against the installed
framework assemblies. For every reached method it records the portable
compiler's own verdict. Results go to `artifacts/winforms-census/`:

- `census.json` lists every reached method with its assembly, MethodDef row,
  the edge that reached it, static feature tags, any P/Invoke import, and the
  compiler status with its [assembly, MethodDef, IL offset] location.
- `census-summary.json` holds the same rankings without the per-method list.
- `summary.md` holds the ranked tables.

Neither the analyzer nor its outputs are part of the compiler or the browser
runtime. It uses System.Reflection.Metadata for the graph. It calls the
portable compiler through the same `src/PortableCompiler` and
`src/AssemblyReader` sources that the self-hosted module is built from.
Nothing runs in a browser, and nothing here demonstrates execution.

## Call graph

The walk is conservative rapid type analysis (RTA) at definition granularity:

- `call`, `newobj`, `jmp` and `ldftn` targets, with MemberRefs resolved through
  type forwarders and MethodSpecs collapsed onto their generic definitions.
- `callvirt` and `ldvirtftn` slots. Each slot resolves to its override or
  interface implementation in every type that reached code instantiates
  through `newobj`, `box`, a `constrained.` call or a delegate allocation.
  That covers explicit MethodImpls, inherited overrides and default
  interface methods.
- The static constructor of every type whose method, constructor or static
  field is reached. BeforeFieldInit is ignored because it can only delay an
  initializer. Module initializers of every assembly that owns reached code
  are included, and so are `Finalize` overrides of instantiated types.

Generic instantiations share their definition. The walk doesn't model
reflection activation (`Activator`, `TypeDescriptor` attributes, resource
deserialization), COM activation or array interfaces that the runtime
synthesizes. Methods that make such calls carry the `reflection: …` tags, so
the gap stays visible. Because the walk is conservative, it reaches
networking, JSON and cryptography code through virtual slots such as
`Stream` and `EventSource`. The per-assembly table gives the first witness
path for each assembly, so a reader can see why each one is included.

## Compiler verdicts

Each reached method with IL is compiled as the only export of a bundle. Its
own assembly is the main image, and the bundle also holds the Stage 95
framework pack and every other assembly that owns reached code. The compiler
lowers the export first and then its callees in registration order:

- **compiles**: the whole closure from that method lowered.
- **blocked in own body**: the first failure is in the method itself, either
  at an IL offset or at method level (signature, locals, exception clauses).
- **blocked in closure**: the method's own body lowered, and the first
  failure is in a callee. That callee site is recorded.
- **export ABI (no body verdict)**: the export boundary rejects interface or
  value-type parameters, value-type results and value-type receivers before
  lowering. Such a method is judged only as some other method's callee.

Every failure site is classified by the instruction at its IL offset and the
resolved operand. Examples are a value-type instance call, interface
dispatch, a static field with an initializer, a call to a P/Invoke, or a
catch-clause type. A method-level failure is classified by its signature,
local and declaration features. The compiler reports only a status and a
location, so a category describes the site. It isn't the compiler's internal
reason.

Static feature tags such as generic methods, P/Invokes by DLL and entry point,
unsafe pointers, pinned locals, explicit layouts, static constructors,
exception filters, threading and synchronization, and reflection are counted
over every reached method. They don't depend on which blocker the compiler
meets first.

## Results

See [results.md](results.md) for the ranked results recorded at this stage.
