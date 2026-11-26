# Stage 72 · Windows reference executable

Run `npm run demo -- stage-72-winforms-windows-reference` on Windows x64
with the .NET 10 SDK and Windows Desktop runtime.

The ordinary executable uses the stock Microsoft Form, TableLayoutPanel,
TextBox, Button and Label and reads an embedded resource. The runner publishes
the original apphost with companion files and a framework-dependent single-file
executable. It starts both on Windows and closes each normally. A separate
harness checks the same managed assembly's real form, captures the initial and
edited images, and records events, geometry, font and DPI.

Each run writes a fresh folder under `artifacts/winforms-windows-reference/`. It contains
both publish outputs, file hashes, SDK and runtime details, screenshots and a
static reference graph including resources, type forwards and P/Invoke
declarations. Static metadata does not establish which imports were executed
or how the CLR bound them. The application here is the Windows baseline for
the browser loader and framework work. The staged runner does not compile or
launch this executable in the browser.
