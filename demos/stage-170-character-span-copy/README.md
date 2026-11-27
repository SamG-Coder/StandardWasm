# Stage 170 · Character span copying

Run `npm run demo -- stage-170-character-span-copy` with the .NET 10 SDK,
the x86 Windows runtime, Node, and Microsoft Edge. Results are written to
`artifacts/stage-170-character-span-copy/results.json`.

The fixture links the real CoreLib two-span and three-span
`String.Concat` bodies. The compiler recognizes the trusted allocation and
internal first-character span construction. The original managed checks,
copies, slices, and return stay in the linked method.

The copy cases cover empty views, overlap within an array and within
linear memory, both directions between those storage kinds, and a typed
argument exception that leaves a short destination unchanged. Array and
linear-memory copies use their corresponding overlap-safe Wasm operations.
