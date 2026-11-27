# Stage 340 · Stock readonly character span empty getter

The compiler resolves the original CoreLib ReadOnlySpan<char>.Empty managed getter and applies the existing compact readonly character span ABI. Its original initobj body supplies the default length and null anchor. The closed reference must name the same CoreLib type in the declaring context and the return signature.

One compiled fixture compares length, IsEmpty, the null anchor, a zero slice, a branch with a nonempty span and the typed IndexOutOfRangeException. The short-span getter retains its explicit diagnostic. Exact unchanged Mono compilation reaches the UTF-16 pointer string constructor in CultureData.JSGetLocaleInfo.

Set STANDARDWASM_COMPILER_PATH to the compiler image and run npm run demo -- stage-340-compact-empty-span. STANDARDWASM_MONO_CORELIB selects the runtime metadata used for linking.
