# Stage 244 · CoreLib Math internal calls

Run `npm run demo -- stage-244-corelib-math` with a built portable compiler, Windows x86 CLR and the stock browser Mono runtime pack. The fixture compares eleven square-root inputs through both the usual .NET Standard MemberRef and the original Mono CoreLib MethodDef. It compares exact non-NaN bits, including negative zero, and checks NaN as a value class.

Authentic Math runtime leaves with exact supported signatures use the existing Wasm instruction mapping. Managed CoreLib math methods retain their original managed bodies.
