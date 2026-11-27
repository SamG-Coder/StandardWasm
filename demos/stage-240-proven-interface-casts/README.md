# Stage 240 · Proven closed interface casts

Run `npm run demo -- stage-240-proven-interface-casts` with an existing portable compiler. The same .NET Standard library returns 967 for preserved reference identity and 971 for a null reference on the Windows x86 CLR, Node and Edge.

The linker keeps the original target identity. When the dynamic cast helper is outside its supported CoreLib interface set, the verifier admits the cast only if every reachable input already has the exact closed target type or is null. The emitted code copies the reference to the result spill. The case whose source retains only its Object type records a located diagnostic.
