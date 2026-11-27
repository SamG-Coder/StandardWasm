# Stage 201 · Default window procedure

Run `npm run demo -- stage-201-default-window-procedure` on Windows with
the .NET 10 SDK, x86 runtime, and Microsoft Edge. The native oracle creates a
hidden borderless popup, forwards its first messages through the real
`CallWindowProcW`, and records the message order, return values, and caption.
Node and Edge run the same checks through the executable host and a Wasm ABI
fixture.

The launcher supplies a resident, instance-local `user32.dll` for Windows
Desktop applications. The named Unicode default procedure has a stable
typed token, and an explicit host provider can supply its own implementation.
`WM_NCCREATE` copies the caption into the window state. The verified popup
creation messages preserve their input structures. Other window styles and
messages have explicit diagnostics until the host implements them. The
fixture registers a host callback. The original managed callback still
requires the reverse interop bridge.
