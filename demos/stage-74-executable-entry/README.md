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
