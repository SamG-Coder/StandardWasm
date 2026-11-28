# Stage 286 · Copy exact managed value vectors

This fixture runs the same .NET Standard library on Windows x86 CLR, Node, and Edge. It uses both original `Array.Copy` overloads with exact nominal struct arrays. The compiler keeps existing destination slots attached to their managed references and copies each immutable struct snapshot independently. A rightward overlap reads from the end. A leftward overlap reads from the start.

```text
npm run demo -- stage-286-value-vector-copy
```

The expected outcomes are 101, 103, 107, 109, 113, 127, 131, 137, 139, 149, 151, and 157. Cases cover reference fields, overlap in both directions, destination aliases, independent arrays, untouched default elements, empty copies, typed validation order, no mutation on failure, and the existing primitive vector path. Set `STANDARDWASM_MONO_RUNTIME` to the matching runtime pack's `runtimes/browser-wasm` directory to verify Mono metadata. The tenth outcome is 147 on stock Mono because it checks both negative indices before the source length.

This stage covers zero-based vectors with the same nominal value element type. Multidimensional arrays, boxing across unlike element types, and primitive widening are outside this stage.
