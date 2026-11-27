# Stage 253. Constraints that name another type parameter

Run `npm run demo -- stage-253-dependent-type-constraints` to compare the
same .NET Standard fixture on Windows x86 CLR, Node and Edge.

The compiler substitutes the closed parameter in `where TImplementation :
TService` before checking the bound. The fixture covers a derived class, an
inherited interface, an exact primitive, a boxed struct or primitive bound,
and a bound declared by an enclosing generic type. The check uses the
original metadata identity, including where distinct types share Wasm storage.
