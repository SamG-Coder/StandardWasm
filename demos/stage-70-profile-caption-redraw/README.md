# Stage 70 · Profile preview redraw

Run `npm run demo -- stage-70-profile-caption-redraw` on Windows with
Microsoft Edge and WebGPU. The runner builds the compiler, compiles the
shared Profile DLL in Edge, and checks the Profile against native WinForms.

Once the first frame has initialized the text layer, an edit clears and
rasterizes only the clipped preview caption. The button glyphs are retained
and the existing preview-only texture upload remains in use.

The same-device benchmark compares full WebGPU redraw, partial upload only,
and partial redraw plus upload, alongside Canvas 2D. Exact pixel checks
compare the WebGPU paths for accented and emoji text, cleared text, and a
maximum-length label. The report records caption count, text update and CPU
submission times at `artifacts/profile-caption-redraw/results.json`.
