# Stage 242 · Closed Type comparisons through a local

Run `npm run demo -- stage-242-closed-type-local` with the built portable compiler. The ordinary .NET Standard fixture checks equality and inequality in closed methods plus successful use of a local Type descriptor on both surviving branch directions. It returns 1009, 1013, 1106 and 1205 on Windows x86 CLR, Node and Edge.

The compiler can prove a closed Type comparison after an adjacent store and reload when no other control-flow path enters the sequence. It keeps the original Type producer if surviving code still reads or addresses the local. An authenticated CoreLib cast from a canonical Type descriptor to RuntimeType has the same identity. The fixture's alternate paths leave the original unimplemented open-type operations reachable whenever the comparison cannot be proved.
