# Stage 309 · Active window for a Win32 message queue

The fixture calls `GetActiveWindow` and `SetActiveWindow` through the native HWND ABI and reads the saved P/Invoke error. The browser host stores activation per guest window registry. Removing the active HWND clears the query result.

```text
npm run demo -- stage-309-active-window
```

The same .NET 10 DLL calls Windows User32 on x86 CLR and the browser host in Node and Edge. The cases cover an empty queue, explicit activation of hidden and disabled top-level windows, replacing the active window, child and null arguments, invalid-handle error, removal, and launch isolation. The adapter models the active HWND. Input focus, system foreground and z-order changes, and activation message delivery remain separate.
