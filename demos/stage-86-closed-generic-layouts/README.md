# Stage 86 · Closed generic layouts

Run `npm run demo -- stage-86-closed-generic-layouts` and `npm test`.
The linked dependency declares two closed versions of a generic storage type.
Each identity includes the resolved definition and ordered type arguments.
The compiler substitutes reference arguments into fields, including reference
vectors declared as `T[]`. A second assembly reads and writes the closed
references and exercises array allocation, null, index checks and identity.

The stage supports reference arguments, exact noncovariant array assignments,
and layout of closed reference type signatures. Generic method specialization,
direct calls on generic declaring types, value-type arguments, generic base
classes and nominal generic constraints need separate support.

See the [closed generic layout decision](../../docs/decisions/0029-closed-generic-reference-layouts.md).
