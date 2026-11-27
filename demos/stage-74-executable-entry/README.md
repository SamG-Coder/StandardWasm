# Stage 74 · Run a dropped .NET entry point

Run `npm run demo -- stage-74-executable-entry` on Windows with the .NET 10
SDK, Node, Microsoft Edge and the win-x86 .NET and Windows Desktop runtimes
matching the Windows reference run. Set `STANDARDWASM_DESKTOP_VERSION` to use
another installed pair. The runner rebuilds the self-hosted compiler, publishes the ordinary
console app in both formats, and compares both executions with the original
process exit. Pass `--no-rebuild` to reuse the existing compiler, or set
`STANDARDWASM_COMPILER_PATH` with that flag to test a specific compiler build.

After building the compiler, run `node demos/stage-74-executable-entry/run.mjs --serve`.
Open the printed local URL. Add the publish output or the original single-file
executable and select **Run managed entry**. Edge compiles and runs in a worker.
The existing intake page remains available without the run feature.

If the compiler exhausts its workspace, the launcher retries with the same
uploaded assemblies and increases the buffer from 1 MiB to at most 8 MiB.
The returned diagnostic comes from the final attempt.

The runner accepts parameterless managed entries with an integer exit result
and no runtime imports. [Stage 95](../stage-95-managed-companions/README.md)
covers the launch path for original managed companion DLLs. [Stage 110](../stage-110-console-arguments/README.md)
covers console arguments and streams. This runner also sends the unchanged
Stage 72 WinForms entry through the launcher and compares the complete Node and
Edge compile results with matched 32-bit framework assemblies. WinForms
currently stops during compilation. The runner records the selected framework
hashes and the first form allocation in the original executable. Evidence is under
`artifacts/executable-entry/run-*/results.json`.
