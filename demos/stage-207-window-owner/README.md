# Stage 207 · Window owner identity

Run `npm run demo -- stage-207-window-owner` with the Windows x86 .NET 10
runtime, Node, and Edge. Set `STANDARDWASM_COMPILER_PATH` to verify a frozen
compiler. Evidence is saved to `artifacts/stage-207-window-owner/results.json`.

The compiler binds the exact raw `USER32.dll!GetWindowThreadProcessId`
declaration with return type `uint` and parameters `HWND, uint*`. The
generated module calls the production browser resolver. The same C# DLL
runs against real HWNDs on Windows. The result comparison checks ownership
and last errors without comparing Windows-assigned IDs to browser IDs.

A browser launch has one process ID and one GUI thread ID. Both are stable
for the lifetime of that launch, and each live HWND in its window table has
that owner. The call returns the thread ID and writes the process ID only
when the optional pointer is nonzero and the output fits in guest memory.
Success preserves both error slots. Unknown or destroyed HWNDs return zero
and set system error 1400 without writing the output. A valid HWND with an
out-of-bounds pointer returns zero and sets system error 998. The raw leaf
does not change the saved P/Invoke error.

The original managed generic wrapper uses an initialized temporary and
copies it to its managed caller after the raw call. Managed UInt32 byrefs,
other Windows thread APIs, and multi-threaded guests are separate boundaries.

[GetWindowThreadProcessId](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowthreadprocessid)
