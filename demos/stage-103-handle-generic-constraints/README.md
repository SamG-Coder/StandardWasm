# Stage 103 · Handle generic constraints

A linked .NET Standard library defines a value slot with the compiler-emitted unmanaged marker and a self-referential generic interface constraint. The linker resolves the exact interface definition and substitutes the slot's concrete argument when checking interface implementations. The slot and its argument retain their normal managed field layout.

Run `npm run demo -- stage-103-handle-generic-constraints`. It returns -17 after assigning and copying a closed value slot. The full verifier compares signed boundaries, a transitive interface edge, a CLR-accepted reference-containing value under the compiled marker, and two wrong-interface diagnostics.

This stage checks compiled constraints with registered nominal argument types and exact interface arguments. Further type expressions and variance conversions require additional constraint support.
