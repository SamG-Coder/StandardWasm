# Stage 159 · Routed COM calls

Run `npm run demo -- stage-159-routed-com-calls --no-rebuild` after `npm test` has built the
self-hosted compiler. Set `STANDARDWASM_COMPILER_PATH` to select another frozen compiler.
The runner compares the same ordinary .NET Standard 2.0 fixture on Windows x86 .NET 10,
Node, and Edge and records hashes and results in `artifacts/stage-159-routed-com-calls/`.

The `.NET Standard` fixture reads a vtable slot through a `void**` field and dispatches
through an unmanaged stdcall function pointer. Windows uses a native invocation of the
test container's callback. The browser supplies a registered guest vtable and invokes the
same signature through the host. The scalar and four-byte managed result forms return
`0` and `-2147467259` respectively, and both callbacks receive the original receiver.

The browser verification in Stage 74 separately exercises malformed signatures, unknown
receivers and modified vtables, instance isolation, and mixed native and COM ordinal
ordering. See [decision 0086](../../docs/decisions/0086-routed-com-calls.md) and
[issue #274](https://github.com/SamG-Coder/StandardWasm/issues/274).
