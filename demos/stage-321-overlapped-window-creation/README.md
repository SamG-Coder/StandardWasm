# Stage 321 Overlapped window creation

Run `npm run demo -- stage-321-overlapped-window-creation`. The Windows x86 CLR oracle creates the same kind of hidden top-level HWND used by the reference WinForms application. The Node and Edge witnesses use the production browser host and the native import ABI from Stage 318.

The witnesses compare synchronous callbacks, style words, the original and adjusted rectangles, and the final client size. Default positions are chosen within each desktop, so the witnesses compare callback positions relative to their chosen origin and verify that independently created windows cascade. Explicit positions are compared directly. The stage also checks rejected creation, the default virtual display profile and independent browser launches.

The supported requests are hidden, unowned captioned windows with explicit sizes, including the WinForms reference form's extended styles. Showing the window, creating children and dispatching paint are separate host operations.
