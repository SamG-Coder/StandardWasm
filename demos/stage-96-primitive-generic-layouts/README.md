# Stage 96 · Primitive arguments in closed generic layouts

Run `npm run demo -- stage-96-primitive-generic-layouts` and `npm test`.

An ordinary .NET Standard dependency declares a closed generic struct with
scalar, reference and vector fields. The consumer initializes closed
instances, reads defaults, stores values across the assembly boundary and
checks that later writes leave copied values independent. The runnable
example returns 28.

The CLR oracle checks signed and unsigned scalar and vector argument
identities, packed signed storage, floating-point values and a null receiver.
It also changes the signed field signature to its unsigned equivalent
and verifies that both runtimes reject the field binding.

See [primitive generic argument layout](../../docs/decisions/0036-primitive-generic-layouts.md).
