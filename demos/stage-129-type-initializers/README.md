# Stage 129 · Type initializers and root objects

Run `npm run demo -- stage-129-type-initializers` on Windows with the .NET 10 SDK, the x86
runtime, Node and Microsoft Edge. Build the self-hosted compiler with `npm test` first.

The fixture is an ordinary netstandard2.0 library with a bundled dependency. Each bundle also
carries the supported 32-bit `netstandard`, `System.Runtime`, `System.Threading` and
`System.Private.CoreLib` (decision 0044). The x86 CLR runs the same DLLs as the oracle. It
loads every kernel into a fresh `AssemblyLoadContext`, matching the fresh Wasm instance per
kernel. The self-hosted compiler then compiles each kernel in Node and in Edge and must
return the CLR's value. A `Trace` class without an initializer records which initializers
ran, in order.

The kernels cover:

- a reached initializer running once, while one in an untaken branch never runs
- a precise static call that initializes after its argument (`argument,initializer`)
- a precise static store that initializes after its right-hand side
- a beforefieldinit initializer that runs once before its field is read
- constructors triggering a precise initializer
- an inherited static field initializing only its declaring base
- recursive initialization, where B reads A while A runs
- try, catch and finally inside an initializer
- a field read and a static call into a bundled dependency's initializer
- a static call into a closed beforefieldinit generic type, which reads no static field and so
  needs no initializer
- distinct `new object()` allocations, root-object identity through a static field, and a
  failed nominal test against an unrelated class
- an initializer throwing a managed exception, caught as CoreLib's `TypeInitializationException` on two triggers, and a nested type's failing initializer (#170)

The failure cases run several exports on one instance. The CLR raises the same
`TypeInitializationException` on each trigger. The compiled module traps on each trigger,
runs the initializer once, and keeps an unrelated export working. The nested case
(`OuterFailing` reading `Failing`) marks both types failed. `RawTrapRetired` exhausts the
Wasm stack inside an initializer. The CLR would end the process, so it has no CLR value.
It checks that the next host entry retires the interrupted initializer instead of treating
it as recursion.

`GetHashCode`, `ToString`, `GetType` and `Equals` on a root object, `lock`, and a static call
into a closed precise generic type must return the explicit -2 diagnostic at their instruction. The design is decision 0057.
