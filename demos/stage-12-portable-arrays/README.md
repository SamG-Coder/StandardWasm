# Stage 12 · Portable managed arrays and self-recompilation

From the repository root after installing dependencies:

    npm run demo -- stage-12-portable-arrays
    npm test

The demo compiles Route from TypedArrayLibrary.dll while the compiler runs in Wasm. The generated module returns 516 for (255, 3). It uses packed byte arrays and ordinary i32 arrays, passes them through typed helpers, and carries references through branches. Integer, void and managed array signatures are checked at call sites, local stores and control-flow merges. Array allocation and element operations use Wasm GC directly.

The full suite now uses the complete compiler bootstrap in [Stage 13](../stage-13-complete-bootstrap/README.md).
