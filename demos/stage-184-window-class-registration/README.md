# Stage 184 · Window class registration and procedure replacement

Run `npm run demo -- stage-184-window-class-registration` with the Windows x86
.NET 10 runtime, Node, and Edge. Results go to
`artifacts/stage-184-window-class-registration/results.json`.

The verifier sends the raw Wasm32 Win32 ABI through the production executable
loader. Its C# oracle registers a class and creates a hidden native HWND on
Windows. Both browser engines compare the registered class attributes and
replacement procedure results with the same oracle.

- `RegisterClassW` snapshots a 40-byte x86 `WNDCLASSW` and binds its procedure
  to an explicitly registered callback. Instances own their class tables.
  Within an instance, each name has one atom and each module can register its
  own definition. Duplicate ASCII case variants in a module return 1410.
- `GetClassInfoW` looks up the definition by name or atom. It writes the
  original fixed-width fields and the query's name or atom, and returns the
  class atom as the native success value. Missing classes return 1411.
- `SetWindowLongW` and `SetWindowLongPtrW` replace `GWL_WNDPROC` on an existing
  HWND. The previous procedure remains callable through `CallWindowProcW`.
  `EnableWindow` sends its synchronous messages to the current procedure.
  Successful calls preserve the system last-error slot. The raw imports
  report failures through that slot. The upstream wrappers and declarations
  control when the saved P/Invoke error is captured.

This stage covers application-local classes with numeric menu resources and
existing HWNDs. It leaves built-in and global classes, string menu resources,
other write indices, `UnregisterClassW`, `CreateWindowExW`, and managed reverse
callbacks for their respective platform and compiler boundaries. The fixture
checks the browser's own bounds and callback-token validation separately from
the Windows oracle.

[RegisterClassW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerclassw) ·
[GetClassInfoW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getclassinfow) ·
[SetWindowLongW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowlongw)
