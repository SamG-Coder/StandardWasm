# Stage 260. Interface types used as their own generic bounds

Run `npm run demo -- stage-260-identical-interface-constraints`. The same
.NET Standard assembly returns 101, 103, 107, 109 and 113 in the Windows
x86 CLR, Node and Edge.

The fixture covers an interface passed as its own bound, separate closed
forms of a generic interface on nested classes, ordinary inheritance of
an interface bound, and a concrete implementation.

This check is used for the original CoreLib async state-machine box,
which has a closed specialization whose argument is the bound interface
itself. The compiler checks the assembly, type definition and exact
generic arguments before falling back to the existing interface walk.
