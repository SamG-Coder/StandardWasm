# Stage 296 · Win32 window handle lifetime

The ordinary .NET Standard fixture calls the exact Windows `IsWindow(HWND)` ABI. The Windows x86 oracle supplies owned windows created through User32. Node and Edge run the same fixture DLL through the production browser host and its per-launch window registry.

```text
npm run demo -- stage-296-is-window
```

Use the generated compiler from `npm test`, or set `STANDARDWASM_COMPILER_PATH` to a frozen compiler. The hidden disabled window, visible window, child window and surviving window each return 1. Null, invalid and two destroyed handles each return 0. Separate browser instances isolate their registries. A wrong native word is diagnosed at the import. A wrong nominal wrapper is diagnosed at its call site. The compiled fixture imports only `standardwasm:win32/isWindow`.

Results are written to `artifacts/stage-296-is-window/results.json`.

[Windows IsWindow API](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-iswindow)
