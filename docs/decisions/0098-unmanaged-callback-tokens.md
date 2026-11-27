# 0098 · Unmanaged callbacks as host-callable tokens

Status: proposed with Stage 361 (#274). Claude wrote it on 2026-11-28, taking over
Codex's EnumThreadWindows work. It builds on decision 0078 (function pointers in
table 2) and decision 0083 (typed native imports). The generic WebGPU layer
(decision 0096, #313) is the next intended consumer, for `webgpu.h` callbacks.

## Problem

A Win32 or C API that takes a callback receives a `delegate* unmanaged[Stdcall]<...>`
value. In compiled code this value is a table-2 slot (decision 0078). The callee is
usually a static `[UnmanagedCallersOnly]` method, which keeps its lowered managed
signature. A Foundation wrapper such as `HWND`, `LPARAM` or `BOOL` is a GC struct
there, which host JavaScript cannot construct or read. The host therefore cannot
call the slot's function directly.

## Decision

For each distinct callback signature, the compiler emits one synthetic **entry**
function:

```
entry(i32 target, word p1, ..., word pn) -> word
```

- Each `word` is an i32 on wasm32. Every parameter and the result must be `int`,
  `nint`, or a sequential blittable struct with exactly one `int` or `nint`
  instance field (`UnmanagedCallbackSignatures.AbiWord`). A `void` result is
  allowed.
- The entry boxes each word into the callee's lowered type (`struct.new` for a
  wrapper) and calls `target` through the signature's decision 0078 carrier with
  `call_indirect`. It then unboxes the result. A null wrapper reference is
  materialized as the default value first.
- The entry itself has a table-2 slot, so it can be loaded as a funcref with
  `table.get 2`. No table is exported.
- An import that takes a callback receives the **token** as two arguments: the
  i32 target slot and the entry funcref. The host calls `entry(target, ...words)`.
  `standardwasm:win32/enumThreadWindows(thread, target, entry, payload)` is the
  first such import.

The plan (`UnmanagedCallbackPlan`), the signature reader and the entry emitter
live in `src/PortableCompiler/UnmanagedCallbacks.cs` and name no particular API.
An import binding reads its callback with `UnmanagedCallbackSignatures`, applies
its own nominal ABI checks (for example, WNDENUMPROC requires BOOL, HWND and
LPARAM), and calls `MethodCompiler.RegisterUnmanagedCallback`.

## Calling conventions

The reader accepts `ELEMENT_TYPE_FNPTR` with explicit stdcall (`0x02`) or with
the platform-default unmanaged convention (`0x09`, which is stdcall on Windows
x86). Any other convention stays unmatched, so the import keeps its located `-2`
diagnostic. The Stage 361 fixture checks this with a cdecl declaration.

## Semantics that differ from the CLR

- **Escaping exceptions.** The CLR fail-fasts when an exception escapes an
  `UnmanagedCallersOnly` method. The entry wraps the call in
  `try_table (catch_all)` and executes `unreachable`, so the launch ends with a
  trap that managed `catch` and `finally` cannot intercept. The cost: a host
  `Error` thrown by a nested import inside the callback also becomes that trap,
  and its message is lost. Stage 361 checks this with a checked-overflow callback.
- **Mismatched or null targets** trap in `call_indirect`, as in decision 0078.
- **Synchronous use only.** The host may call an entry only while it is still
  inside the import that received the token. That is enough for EnumThreadWindows,
  which runs during managed execution and therefore stays inside the current
  initializer epoch (decision 0057). A token kept for a later asynchronous call
  (a `webgpu.h` request callback, for instance) would have to enter through the
  decision 0057 export-entry wrapper. That retention is not implemented. A
  consumer that needs it must add the wrapper and a fixture first.

## Engine requirements

The entry uses reference types (`table.get`, funcref parameters), GC structs and
exception handling with `try_table`. The module already requires all of these.
