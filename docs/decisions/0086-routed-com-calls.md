# 0086 · Routed COM calls

## Context

WinForms uses generated Win32 bindings for Windows COM. The bindings load a vtable
entry from a native receiver and execute an unmanaged `calli`. For example,
`IOleControlSite.OnControlInfoChanged` invokes slot 3 and preserves its HRESULT.
Browser-hosted modules address wasm32 guest memory. A browser platform service can
expose an interface by registering an instance-local guest receiver and its slots.

## Decision

The first routed shape is stdcall with a wasm32 pointer parameter and a 32-bit integer
result. A measured sequential four-byte managed result with an `int` payload is
reconstructed in its nominal layout so surrounding managed IL sees the proper type.
The emitted module runs the original receiver and vtable loads and forwards the loaded
slot token and original receiver to `standardwasm:com` function `stdcall:i32(p32)`.
The guest accesses the same imported linear memory as the other platform services.

The browser registry is owned by the platform host. It allocates guest receivers and
vtables, issues opaque slot tokens, and validates the registered receiver, signature,
and current vtable before invoking a registered callback. An unavailable capability
raises an explicit host error. COM dispatch uses the compiler's existing dynamic
import table for ordering. The COM registry has its own capability and error semantics,
and leaves the Win32 and P/Invoke last-error slots alone.

Stage 159 verifies both return representations and receiver identity against an
actual Windows stdcall vtable. Stage 74 verifies the registry and import contract
in isolation. Additional native signatures, WinForms interface acquisition and
lifetime, and the remaining platform methods are tracked under
[issue #274](https://github.com/SamG-Coder/StandardWasm/issues/274).
