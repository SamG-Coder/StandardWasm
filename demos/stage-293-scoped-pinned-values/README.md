# Stage 293 · Scoped pinned value references

This .NET Standard fixture compares Windows x86 CLR, Node and Edge for a synchronous pointer call borrowing a reference-free struct. Its managed reference supplies the initial bytes. The compiler lends a frame-owned native stack slot to the original pointer callee and publishes the resulting fields back to the same managed location before returning. The return value and writes are independent, so a zero status still publishes writes.

```text
npm run demo -- stage-293-scoped-pinned-values
```

The four positive cases return 101, 103, 107 and 109. They cover local references, array elements, class fields, repeat calls and default fields. The escaping pointer case retains a located diagnostic. Set `STAGE293_FIXTURE_FRAMEWORK` to `net10.0` to add an authentic `Unsafe.NullRef` case that returns 113 after the pointer callee raises the expected typed exception. Set `STANDARDWASM_MONO_RUNTIME` to a matching pack's `runtimes/browser-wasm` directory for Mono metadata.

The compiler checks the exact pinned local and pointer signature. A same-owner managed callee can access its verified integer fields and return scalar data. The original Win32 rectangle path requires the documented synchronous native import and its genuine last-error calls. Pointer retention, callbacks, managed fields and wider pinning patterns stay outside the proof. The browser host supplies window geometry separately.
