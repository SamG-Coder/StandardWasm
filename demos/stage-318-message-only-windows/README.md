# Stage 318 Message-only windows

Run `npm run demo -- stage-318-message-only-windows`. The Windows x86 CLR oracle creates two hidden windows under `HWND_MESSAGE` and a third whose procedure rejects creation. Node and Edge run the corresponding native ABI through the production browser launcher.

The comparison covers the WinForms parking-window style, the resolved parent handle, the incoming and adjusted rectangles, callback order, the default procedure and focus. The browser also checks that separate launches have separate window state and that the parking window starts with virtual display defaults when the embedding host supplies no metrics.

This stage covers hidden overlapped requests with explicit coordinates and the message-only parent sentinel. The visible form, controls, and paint delivery have their own host paths.
