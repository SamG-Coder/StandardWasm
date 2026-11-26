# Stage 122 · Runtime class dispatch

Run `npm run demo -- stage-122-runtime-class-dispatch`. A linked factory's
virtual override creates a new receiver class. That class's original
virtual method returns 85 for the demo input of 5.

The CLR comparisons in `npm test` also cover base and derived instances
returned by a linked property, a new slot that hides the base slot, and a
typed null exception. The null-only case exercises a call site with no
possible non-null managed receiver.

See the [runtime class dispatch decision](../../docs/decisions/0050-runtime-class-dispatch.md).
