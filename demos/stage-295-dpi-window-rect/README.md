# Stage 295 · DPI-aware window rectangle adjustment

The ordinary .NET Standard fixture calls the real Windows `AdjustWindowRectExForDpi` pointer ABI on an x86 CLR. The same compiled assembly runs through the browser launcher and its Win32 platform adapter in Node and Edge. The adapter updates the original RECT in place using the measured reference desktop caption, frame, menu and edge metrics.

```text
npm run demo -- stage-295-dpi-window-rect
```

Use the generated compiler from `npm test`, or set `STANDARDWASM_COMPILER_PATH` to a frozen compiler. The 4,904 cases check signed edges, four frame types and common style combinations, menu state, tool and client edges, and zero and positive DPI. The low-DPI matrix covers every DPI from 1 through 95 with ordinary captions, tool windows and captionless windows, with and without a menu. A wrong pointer declaration remains a located compile diagnostic. The browser host reports invalid addresses and a zero DPI without changing rectangle storage. The reference profile models the measured Windows desktop theme. Custom caption fonts and non-client metrics can change the native result.

Results are written to `artifacts/stage-295-dpi-window-rect/results.json`. The adjacent oracle file records all Windows outcomes.
