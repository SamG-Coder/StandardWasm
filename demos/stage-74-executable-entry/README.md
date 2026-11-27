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

The activation-context fixture supplies the addresses of a local native word
and a local blittable handle. Windows, Node, and Edge agree on the activation
lifecycle. Separate cases verify that writes through a pointer update the
managed local, and writes to the local are visible through the same pointer.

The window-state fixture compares `IsWindowEnabled` and `EnableWindow` with
Windows for enabled, disabled, null, and destroyed handles. It checks five
successive enable and disable requests, their previous-state return values,
and the state visible during synchronous `WM_CANCELMODE` and `WM_ENABLE`
notifications. The browser host keeps this state per application instance.
[IsWindowEnabled](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-iswindowenabled)
[EnableWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enablewindow)

The scoped-span fixture stores an array-backed `Span<char>` in a `ref struct`
and copies the struct. Writes through the copy still reach the original array.
The CLR, Node, and Edge compare the alias, the length of a default span, and
UTF-16 values at the wraparound boundary. The same compiled module exercises
a pointer-backed span over the shadow stack, a readonly view over the same
memory, and typed exceptions for a negative length and invalid indices.
The by-reference case mutates an integer field and replaces a span in the
caller's scoped struct. The caller observes both updates while its earlier
value copy still refers to the original array. The delegate fixture then
compares static and instance `SpanAction<char, nint>` targets, in-place span
writes, and a typed null-call exception. It also verifies the order and
removal of static `EventHandler` callbacks.

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
caller that compares the result as a `System.Type`. The original CoreLib
`String.StartsWith(char)` body also runs against the browser's UTF-16
storage, including embedded NUL, empty, and null-receiver cases. The static
byte span probe uses the original PE's readonly data and
checks unsigned byte values and typed bounds failures. A readonly location
keeps that backing field alive across calls and supports byte offsets and
unaligned little-endian reads. The compiler tests these operations directly
and through the original CoreLib Unicode tables used by `Char.IsWhiteSpace`.
The string trim check runs the original CoreLib trimming logic. Its verified
substring helper copies UTF-16 into a fresh Wasm GC backing array. The checks
cover reference identity, Unicode whitespace, a surrogate pair and embedded NUL.

The monitor probe checks the CLR's reentrant lock behavior on the launch
worker's single managed execution agent. It verifies lockTaken and typed
failure paths. Monitor wait and pulse and scheduling across multiple
managed threads require separate support.

The span probe verifies a single-object read-only span over an argument or
local. The span reads a traced cell shared with the original frame slot. Both
views of an argument observe later writes, and indexing still checks bounds.

The debugger fixture links the original .NET 10 `Debugger.IsAttached` getter.
The browser host reports detached by default and can supply a managed
debugger state through `standardwasm:runtime`. The test checks detached and
attached transitions in one instance and runs alongside the monitor import.
