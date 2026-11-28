# Stage 223 · Win32 process window station

Run `npm run demo -- stage-223-window-station` with Windows x86 .NET 10,
Node and Edge. The same compiled fixture calls the real USER32 leaves on
Windows and the production Win32 loader in both Wasm engines. Results are
written to `artifacts/stage-223-window-station/results.json`.

Each guest launch has a stable borrowed window-station handle. The
`UOI_FLAGS` query writes the three original `USEROBJECTFLAGS` fields.
`WSF_VISIBLE` reflects whether the launch provides the Win32 desktop
surface. It is independent of whether the browser tab has focus.

The fixture checks the nominal Windows SDK and scalar signatures, the
optional length output, a short buffer, pointer bounds, output aliasing
and the system and saved P/Invoke error slots. Unrecognized query modes
stay scoped to explicit diagnostics, and a mismatched pointer type is
rejected before emission. The compiler retains the original WinForms interactivity
check and cache.

[GetProcessWindowStation](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getprocesswindowstation) ·
[GetUserObjectInformationW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getuserobjectinformationw)
