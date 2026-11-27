# Stage 263 · Canonical String generic constraints

Closed generic type and method checks read the bundled CoreLib's actual String interface declarations. The existing TypeSpec identity path keeps the declaring assembly, interface definition and closed arguments. The same lookup serves a direct or inherited interface edge.

Run `npm run demo -- stage-263-canonical-string-constraints`. Set `STANDARDWASM_COMPILER_PATH` to verify a frozen compiler. The runner compares the compiled C# positive methods in Windows x86 CLR, Node and Edge, including nested lexical substitution. It changes two individual fixture token arguments to create an invalid closed interface argument and an invalid assembly owner. The CLR rejects both mutated methods, and the Wasm compiler must diagnose their token locations. Results go to `artifacts/stage-263-canonical-string-constraints/results.json`.
