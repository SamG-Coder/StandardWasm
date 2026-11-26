# Stage 62 · Null references at branch joins

Run `npm run demo -- stage-62-null-stack`. Calling `Loop(4)` returns `20`.
The loop alternates between a class instance and a CLI null literal at
the same stack depth. The null path must clear the typed Wasm spill from
the previous iteration.

The CLR, Node and Edge suite also compares reversed branch layouts,
linked class references, strings and arrays. Taking the null arm of a
dereference produces a typed `NullReferenceException`.
