# Stage 209 — GUI thread handle lifecycle

This fixture follows the WinForms thread context's kernel handle lifecycle.
The same SDK-shaped C# assembly runs on Windows x86 and as WebAssembly
through the production Node and Edge executable host.

After building the compiler, run on Windows with x86 .NET and Edge.

```powershell
npm run demo -- stage-209-thread-handle-lifecycle
```

The comparison checks the two current pseudo handles, same-access
duplication, duplicate ownership and closing, the live exit status,
32-bit output storage, and raw and saved last errors. Browser checks
add launch isolation, equality between the current and window-owner
thread IDs, output bounds, and link-time rejection of undersized
pointees. Results are in
`artifacts/stage-209-thread-handle-lifecycle/results.json`.

The browser host represents one live GUI thread per launch. The duplicate
table owns process and GUI-thread handles for the lifetime of that launch.
The supported duplication mode is local, non-inherited, same-access
duplication.
