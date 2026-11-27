# Stage 361 · Thread window enumeration

Run `npm run demo -- stage-361-enum-thread-windows`. It uses `artifacts/StandardWasm.Compiler.wasm`, or the compiler named by `STANDARDWASM_COMPILER_PATH`.

`USER32.EnumThreadWindows` binds to one memory-free import, `standardwasm:win32/enumThreadWindows(thread, target, entry, payload)`. The original stdcall callback crosses as a [decision 0098](../../docs/decisions/0098-unmanaged-callback-tokens.md) token. It consists of the callback's table-2 slot and a compiled entry for the `BOOL(HWND, LPARAM)` signature. For each live top-level HWND of the launch's current thread, the host calls `entry(target, hwnd, payload)`. The entry wraps the words in the callee's HWND and LPARAM types and makes a typed indirect call. A zero BOOL stops the enumeration. No table is exported.

The .NET 10 test assembly uses a real `UnmanagedCallersOnly` method. Windows x86 CLR, Node and Edge compare these behaviors:
- ordered callbacks
- early refusal
- HWND lifetime
- empty, invalid and foreign thread behavior
- all bits of a negative LPARAM

The browser also checks launch isolation and rejects a cdecl declaration with a located diagnostic. It also checks that an exception escaping the callback ends the launch with a trap instead of unwinding into the caller, as the CLR's fail-fast does. On the tested Windows build the API returned true with no visited windows, although Microsoft Learn documents a false result for an empty enumeration.

The host follows default z-order in the existing hidden-window creation lifecycle, which is reverse creation order. When several windows have mixed visibility, or activation has reordered them, the host raises an explicit diagnostic until it tracks z-order. Message-only and child windows are excluded.

[Microsoft Win32 documentation](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumthreadwindows)
