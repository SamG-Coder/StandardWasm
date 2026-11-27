# Stage 359 · Window visibility

Run `npm run demo -- stage-359-window-visibility --no-rebuild`. The genuine `BOOL IsWindowVisible(HWND)` import consults the launch's HWND registry. A window and every parent in its child ancestry must have `WS_VISIBLE`. The query is based on styles even when another window covers it. An owned popup is a top-level window with its own visibility.

The same .NET Standard fixture compares hidden and visible windows, covering windows, child and grandchild ancestry, invalid and stale handles, and an unchanged visible window on Windows x86 CLR, Node and Edge. Browser tests also check launch isolation and reject incompatible raw words and nominal wrappers.

[Microsoft Win32 documentation](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-iswindowvisible)
