# Stage 12 · Portable managed arrays and self-recompilation

From the repository root after installing dependencies:

    npm run demo -- stage-12-portable-arrays
    npm test

The demo compiles Route from TypedArrayLibrary.dll while the compiler runs in Wasm. The generated module returns 516 for (255, 3). It uses packed byte arrays and ordinary i32 arrays, passes them through typed helpers, and carries references through branches. Integer, void and managed array signatures are checked at call sites, local stores and control-flow merges. Array allocation and element operations use Wasm GC directly.

The full suite also recompiles the compiler. The original Wasm instance compiles the reachable Compile entry point from its own CLR DLL. That generated compiler compiles the same entry point again. The modules must have zero imports and match byte for byte. The second generated compiler then reruns every generated-code case from stages 09 through 12.

The original Wasm instance supplies byte-array transport exports for the bootstrap harness. The produced compiler exports a single run function. The harness moves typed references across instances and invokes run to compile. This phase covers the portable compiler fixture suite. A standalone compiler exporting its own transport functions and support for more of .NET Standard remain open.
