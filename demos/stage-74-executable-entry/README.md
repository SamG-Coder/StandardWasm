# Stage 74 · Run a dropped .NET entry point

Run `npm run demo -- stage-74-executable-entry` on Windows with the .NET 10
SDK, Windows Desktop runtime, Node and Microsoft Edge. The runner rebuilds
the self-hosted compiler, publishes the ordinary console app in both formats,
and compares both managed entry executions with the original process exit.

After building the compiler, run `node demos/stage-74-executable-entry/run.mjs --serve`.
Open the printed local URL. Add the publish output or the original single-file
executable and select **Run managed entry**. Edge compiles and runs in a worker.
The existing intake page remains available without the run feature.

The runner accepts parameterless managed entries with an integer exit result
and no runtime imports. [Stage 95](../stage-95-managed-companions/README.md)
covers the launch path for original managed companion DLLs. Console streams,
arguments, and execution with a browser framework require separate work. It also sends
the unchanged Stage 72 WinForms entry through the same compiler path. The
compiler currently rejects it. An independent metadata probe records the first
form allocation in the entry and its upstream base class; the current compiler
status does not include an IL offset. Evidence is under
`artifacts/executable-entry/run-*/results.json`.

The browser loader accepts the Win32 module lookup and last-error host imports
for a single managed execution instance. Module lookup uses the original
selected executable's virtual path and a stable process-local handle. A
file-backed companion with neutral PE manifest resource 2 also receives a
handle. Bundled companions keep the managed manifest fallback. Unknown modules
return a failed lookup. The system error and cached P/Invoke error occupy
separate slots, and lookup uses the same linear memory as other imports.
The browser host can create an activation-context handle from a file-backed
module's numeric, language-neutral manifest resource and maintain the active
contexts in stack order. The current contract retains the original manifest
bytes. The compiler links the native creation, lookup, activation,
deactivation, and release entries with their scalar and SDK HANDLE or BOOL
shapes. Side-by-side dependency resolution and file-path manifests are still
pending.

The browser host models the Win32 DPI awareness context for one process and
one managed thread per execution. The managed fixture compares context
validation and queries, modern and legacy setters, and error handling with
fresh Windows processes. Browser viewport scaling, per-monitor DPI changes,
and manifest-defined initial awareness are outside this contract.

Run `node demos/stage-74-executable-entry/verify-win32-host.mjs` on Windows to
compare the host import contract in Node and Edge against native .NET calls.
The probe invokes the imports through Wasm. Pass the path to the self-hosted
compiler as the first argument to also compile the managed Marshal fixture
and compare module lookup and error transfer with the CLR. It covers a native
pointer signature and the SDK's one-field PCWSTR and HMODULE value shape.
The original WinForms call still needs support for its pinned-string wrapper.

Run the Windows oracle with the x86 and x64 .NET Desktop runtimes to record
the native activation-context contract for the installed WinForms assembly.

```powershell
dotnet run --arch x86 --file demos/stage-74-executable-entry/Win32HostOracle.cs
dotnet run --arch x64 --file demos/stage-74-executable-entry/Win32HostOracle.cs
```

The oracle selects the embedded manifest by numeric resource ID. It records
the native structure offsets, the active context before and after
deactivation, and the errors for a missing manifest and invalid flags.

The managed probe also checks pinned addresses of blittable static structs.
A direct managed store and a pointer store use the same stable linear-memory
storage, including after a static initializer and across separate exports.

The managed Boolean probe takes the address of local Boolean slots and
passes those locations through ordinary `ref bool` and `out bool` calls. It
checks that direct assignments and aliased reads still see the same value,
including when the slot also serves as `Monitor.Enter`'s lock flag. The
probe also exercises a Boolean cell and a single-object span in one frame. The generic
fixture specializes `ref T` and `out T` with Boolean and string. Its checks
cover loads through the location, copies between locations, and overwriting
a live slot with `default(T)`. A bundled probe also constructs a closed
generic value, initializes its own readonly fields, and invokes its
original read-only getters on populated and default values.

The constrained-reference probe covers a base implementation, an override,
a null receiver, and reference identity after `box T` for a reference-type
specialization. The type-descriptor probe checks that `typeof` and `GetType`
share one descriptor per closed generic type and that different type
arguments produce distinct descriptors. The bundled struct-alias probe also
copies and replaces a struct through generic `ref T` and `out T`, including
resetting the original slot to its default value. The CoreLib dispatch probe links
Microsoft's `System.Object.Equals` body and compares inherited equality,
a managed override, and the null-receiver exception. This probe uses the
matching installed win-x86 .NET 10 runtime pack (decision 0044). The
generic member probe invokes original generic methods on a struct location,
including writes that remain visible to the caller, and checks that a generic
reference local initializes to null. A CoreLib-backed fixture also verifies
that a direct method can return a canonical type descriptor or null to a
caller that compares the result as a `System.Type`.

The monitor probe checks the CLR's reentrant lock behavior on the launch
worker's single managed execution agent. It verifies lockTaken and typed
failure paths. Monitor wait and pulse and scheduling across multiple
managed threads require separate support.

The span probe verifies a single-object read-only span over an argument or
local. The span reads a traced cell shared with the original frame slot. Both
views of an argument observe later writes, and indexing still checks bounds.
