# Stage 99 · Named value arguments

The compiler resolves sequential struct arguments in closed generic signatures to the defining assembly and TypeDef. The consumer loads two layouts with the same field types and different nominal identities from a separate .NET Standard 2.0 library.

Run `npm run demo -- stage-99-named-value-arguments`. The generated module returns 19 for input 5. The full suite checks default initialization and copied values at signed integer boundaries. It also substitutes the other struct in a linked field reference, checks the CLR rejection and checks the compiler diagnostic at the field load.

The original WinForms property store depends on a named struct with an inner explicit-layout union. Supporting overlapping CLR fields is the next step for that layout.
