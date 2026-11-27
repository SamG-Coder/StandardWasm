# Stage 203 · Redraw requests for hidden windows

Run `npm run demo -- stage-203-hidden-window-redraw` on Windows with the
.NET 10 SDK, x86 runtime and Microsoft Edge. The fixture uses the Windows SDK
shapes for `RedrawWindow`. Windows invokes the raw P/Invoke on a hidden native
popup. StandardWasm compiles the same fixture for Node and Edge, which invoke
the matching production browser import.

The checks cover full-window and rectangle invalidation, synchronous update
flags, validation, and the flag combinations used by WinForms control and
form paths. Hidden top-level windows dispatch no paint messages. Valid calls
preserve both last-error slots and the borrowed rectangle. A retired handle
sets system error 1400 and leaves the saved P/Invoke error intact.

Visible windows and region handles require dirty geometry and a paint queue.
The host reports error 50 for those requests, desktop-wide invalidation,
and unimplemented flags. It reports error 998 when the rectangle would lie
outside guest memory.
