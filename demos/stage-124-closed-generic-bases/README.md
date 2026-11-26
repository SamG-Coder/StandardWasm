# Stage 124 · Closed generic bases

Run `npm run demo -- stage-124-closed-generic-bases`. The linked fixture
updates an inherited field on a closed generic base, follows a recursive
derived field, and returns 12 for input 5.

`npm test` compares same-assembly and linked closed base classes with the
CLR. It covers shared and distinct base instantiations, substitution inside
an array argument, inherited field access, base assignment, and runtime
dispatch of an ordinary virtual slot through the generic base.

See [decision 0052](../../docs/decisions/0052-closed-generic-bases.md).
