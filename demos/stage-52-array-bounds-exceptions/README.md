# Stage 52 · Typed array bounds exceptions

Run `npm run demo -- stage-52-array-bounds-exceptions`. The generated
`Rethrow(0, -1)` returns `-202` after the outer catch receives the original
`IndexOutOfRangeException`.

Portable array loads and stores guard the index before accessing the GC
array. Stage 52 covers empty arrays, valid endpoints, both bounds, packed
bytes, 64-bit stores, propagation through a call, and nested matching and
rethrow. Null array accesses and invalid allocation lengths retain their
existing trap behavior.

Run `npm test` for the CLR, Node and Edge comparisons and generated-compiler
replay.
