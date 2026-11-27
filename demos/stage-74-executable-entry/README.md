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
Native activation-context creation still needs a browser implementation.

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
