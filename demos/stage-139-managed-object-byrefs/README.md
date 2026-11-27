# Stage 139 · Managed object byrefs

Run `npm run demo -- stage-139-managed-object-byrefs`. An ordinary .NET Standard library passes an element of a managed `object[]` into another method by reference. The demo returns 7 after a write and a read that preserve object identity and leave the other element unchanged.

The CLR comparison suite tests two arguments aliasing the same element, two distinct elements, a reference local redirected to a different array, writes visible to a caller after exception unwinding, and null or invalid indices checked before the next argument is evaluated. See [decision 0067](../../docs/decisions/0067-managed-object-locations.md).

The suite also covers `out` and `ref` on locals and parameters typed as a class. It checks that a write updates the addressed slot, that two aliases observe the same slot, and that replacing a parameter leaves the caller's reference unchanged.
