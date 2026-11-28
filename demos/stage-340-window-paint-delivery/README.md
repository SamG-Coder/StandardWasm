# Stage 340 · Window paint delivery

Run `npm run demo -- stage-340-window-paint-delivery` on Windows with the .NET 10 SDK, the x86 runtime, Node and Microsoft Edge. Build the self-hosted compiler first.

The same managed fixture runs on the Windows x86 CLR and through the StandardWasm launch host in Node and Edge. It uses the WinForms SDK shapes: `HWND`, `HDC` and `BOOL` handle structs, `RECT`, and the 64-byte x86 `PAINTSTRUCT` with its fixed-buffer `rgbReserved`. `operations.json` lists 106 operations. The Windows oracle replays them on real desktop windows, two visible tool popups and one hidden one. The runner replays them on guest windows with the same styles, client sizes and window-procedure results.

The comparison covers:
- **Update regions.** These are exact unions and differences, not bounding boxes. A partial `ValidateRect` leaves the correct remainder. `GetUpdateRect` and `rcPaint` report the region's bounding box. Rectangles are clipped to the client. Inverted rectangles are normalized by both `InvalidateRect` and `ValidateRect`.
- **WM_PAINT synthesis in PeekMessageW.** It comes only after posted messages. It is not removed by `PM_REMOVE`. It honours the HWND, range, `QS_POSTMESSAGE` and `QS_PAINT` filters. It goes in z-order, newest window first. It stops once the region is validated or the window is destroyed.
- **Erasing.** An erase request is kept only for a non-empty clipped area and is cancelled when the region empties. `BeginPaint` and `GetUpdateRect(..., TRUE)` send `WM_ERASEBKGND`: BeginPaint passes its own HDC, GetUpdateRect a temporary one. `PAINTSTRUCT.fErase` stays set after a zero `WM_ERASEBKGND` result, until a later erase succeeds.
- **BeginPaint and EndPaint.** This includes clean, hidden and invalid windows. BeginPaint zeroes the reserved bytes. An invalid HWND leaves the structure untouched with `ERROR_INVALID_WINDOW_HANDLE`. A successful call preserves the last error.
- **Fixed-buffer sizeof.** The x86 CLR and Wasm agree on size, scalar field addresses and writes, and a fixed-buffer element read through a native pointer (decision 0095). The direct indexing case on a managed local retains its located diagnostic.

The browser also checks:
- Null-HWND desktop invalidation and validation, and visible children or parents with children, return `ERROR_NOT_SUPPORTED`.
- Out-of-memory `PAINTSTRUCT` and `RECT` pointers return `ERROR_NOACCESS`.
- Launches are isolated from each other.
- An `int*` passed where a `PAINTSTRUCT*` is expected is a located diagnostic.

ShowWindow, visible child windows and `WS_CLIPCHILDREN`, DispatchMessageW, RedrawWindow on shown windows, and HRGN APIs are separate work. Paint order after activation needs z-order tracking and throws a diagnostic when it would matter.
