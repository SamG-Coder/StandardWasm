# Stage 189 · Synchronous Win32 window creation

Run `npm run demo -- stage-189-window-creation` with the Windows x86 .NET 10
runtime, Node, and Edge. The verifier sends the raw twelve-argument Wasm32
`CreateWindowExW` ABI through the production executable loader. It compares
against real User32 windows and saves the full traces in
`artifacts/stage-189-window-creation/results.json`.

The host resolves an application-local class by name or atom, publishes the
HWND before the first callback, and reads the current procedure for each
message. Popup creation sends `WM_NCCREATE`, `WM_NCCALCSIZE`, `WM_CREATE`,
`WM_SIZE`, and `WM_MOVE`. Style-zero overlapped creation starts with
`WM_GETMINMAXINFO` and ends at `WM_CREATE`. The browser platform can supply
`windowCreationMetrics`, the ten signed 32-bit fields of its virtual
`MINMAXINFO`, to enable overlapped creation. Track dimensions and modified
client rectangles are applied where the corresponding native path uses them.

`CREATESTRUCTW` carries the original request, including the opaque creation
parameter and the Unicode caption. It shares an address between
`WM_NCCREATE` and `WM_CREATE`, and is written again before `WM_CREATE`.
Each synchronous creation has its own guest-memory scratch page until it
returns. Nested creation and guest memory growth leave the outer pointers
valid. The host dispatches the observed cancellation and teardown messages
and removes the pending HWND after rejection. A thrown callback also removes
the pending HWND and propagates the original error.

Coverage is limited to hidden top-level popups and style-zero overlapped
windows with explicit geometry, no parent or menu, and a registered browser
procedure. Other styles, default placement and message-only windows report
an unsupported operation. The current stage verifies the host ABI. Compiler
bindings, managed reverse callbacks, default window procedure behavior,
normal destruction and rendering have separate milestones.

[CreateWindowExW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-createwindowexw) ·
[CREATESTRUCTW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-createstructw) ·
[WM_GETMINMAXINFO](https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-getminmaxinfo) ·
[WM_NCCALCSIZE](https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-nccalcsize)
