# Stage 249. Display device capabilities

Compile the original Win32 display imports used during WinForms startup. The
ordinary .NET Standard fixture calls the same three APIs on Windows and through
the browser host.

Run `npm run demo -- stage-249-display-device-capabilities`. The verifier
compares four display queries, successful release, and null handles with the
Windows x86 CLR in Node and Edge. It uses the Windows display metrics as the
guest profile for the parity check and also checks Edge's own display profile.
Released and foreign browser context tokens are invalid.

The browser models common display device contexts for a single logical display.
Private and class contexts, printer and metafile contexts, and selectors without
an explicit profile value have no compatible implementation in this stage.
The contexts provide display capabilities and do not implement GDI drawing.
