# Stage 150 · Function pointers

Run `npm run demo -- stage-150-function-pointers` on Windows with the .NET 10 SDK, Node and
Microsoft Edge. Build the self-hosted compiler with `npm test` first.

C# 9 function pointers (`delegate*<int, int, int>`) compile to `ldftn` and `calli`. The fixture
is an ordinary netstandard2.0 library built with `LangVersion` 9 and `AllowUnsafeBlocks`.

- **Pointer values.** A pointer is a native int: a slot in a funcref table whose slot 0 is null.
- **Calls.** `calli` becomes `call_indirect`, typed by the call-site signature.

The CLR runs each kernel, and Node and Edge must return its values. The kernels cover:

- choosing among three targets
- an array of pointers in `stackalloc` memory (`sizeof(delegate*)` included)
- a pointer kept in a struct field and passed as an argument
- `long` and `void` signatures, the latter writing through an `int*`
- recursion through a pointer
- equality of two loads of the same method
- null tests

Calling a null pointer is an access violation on the CLR, which ends the process. In Wasm it
is an engine trap, so that case runs in Wasm only.

`delegate* unmanaged[Cdecl]` stays a located diagnostic, since unmanaged calls belong with
P/Invoke (#274). The design is decision 0078.
