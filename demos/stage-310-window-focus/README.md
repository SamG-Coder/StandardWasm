# Stage 310 · Keyboard focus for a Win32 message queue

The fixture uses the native `GetFocus` and `SetFocus` HWND ABI. The browser registry maintains focus and top-level activation separately. It delivers `WM_KILLFOCUS` followed by `WM_SETFOCUS` when focus moves, with the new focus visible during both callbacks.

```text
npm run demo -- stage-310-window-focus
```

Windows x86 runs the same .NET 10 DLL against User32 and records the actual messages from hidden windows. Node and Edge compare the returned HWNDs, focus and active state, ordered messages and saved error for each operation. Cases include nested children, implicit top-level activation, clearing focus, rejected disabled and invalid handles, and destruction. A separate launch starts without focus. Browser keyboard event delivery, foreground policy, and cross-thread queue attachment remain outside this stage.
