# Stage 261 · Hidden window invalidation

The production Stage 74 loader binds the original WinForms Primitives
USER32.dll InvalidateRect leaf. The signature is a native HWND, a nullable
pointer to a 16-byte RECT and a 32-bit BOOL, returning a 32-bit BOOL. The
original managed caller and P/Invoke flags retain their behavior.

Windows x86 leaves an already hidden top-level window's client update region
empty for whole-client and explicit-rectangle invalidation. The stage covers
both erase modes, empty, inverted, clipped and disjoint requests, and repeated
calls. The borrowed input remains unchanged, the raw system error is preserved
on a live window, and the saved P/Invoke slot remains separate. An invalid
non-null HWND produces the observed native return of TRUE with
ERROR_INVALID_WINDOW_HANDLE and leaves the saved error unchanged.

Shown or child windows and a null HWND requesting desktop-wide invalidation
return ERROR_NOT_SUPPORTED through the raw error slot. Real shown windows
accumulate a client update region and later receive WM_PAINT. Supporting that
path requires a corresponding browser region and queue, including erasure
at paint time. The checked browser memory bounds report ERROR_NOACCESS for an
invalid RECT range.

Run the same fixture on Windows x86 CLR, Node and Edge with the generated
compiler from npm test or a frozen STANDARDWASM_COMPILER_PATH.

    npm run demo -- stage-261-hidden-window-invalidation

The Windows check queries the update region and paint queue for the hidden
window. The browser test uses the normal executable host and checks the absence
of callbacks, invalid HWND behavior, the unsupported modes and buffer bounds.
A mismatched raw pointer signature produces a diagnostic at the native
declaration.
