# Stage 167 · Closed generic sizeof

Run `npm run demo -- stage-167-closed-generic-sizeof` with the .NET 10 SDK,
the x86 Windows runtime, Node, and Microsoft Edge. Results are written to
`artifacts/stage-167-closed-generic-sizeof/results.json`.

The ordinary .NET Standard fixture checks `sizeof(T)` for closed method
and declaring type arguments. The compiler resolves the CLI type before
measuring it. In particular, Boolean, Byte, Char, and Int16 can all use a
Wasm i32 evaluation slot and still have different CLI storage sizes.
The fixture also covers wide values, native integers, enum storage,
alignment padding, and an empty struct.
