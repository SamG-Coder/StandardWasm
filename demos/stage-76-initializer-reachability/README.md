# Stage 76 · Initializer reachability

Run `npm run demo -- stage-76-initializer-reachability` and `npm test`.
A genuine .NET Standard executable method calls an independent type in
a dependency that also declares a type initializer. The unused type's
initializer does not prevent compilation.

Node and Edge compare the results with the CLR for plain and bundled
compilation. Since Stage 129 (decision 0057), a reached initializer is
compiled and run, never skipped. These initializers call `int.Parse`, which
the compiler does not support yet. The unsupported diagnostic therefore
points at that call inside the `.cctor`, in the module that declares it. A
genuine module initializer is still diagnosed. Both leave the destination
unchanged. `TypeInitializationException` wrapping remains tracked in
[#170](https://github.com/SamG-Coder/StandardWasm/issues/170).

See [ECMA-335](https://ecma-international.org/publications-and-standards/standards/ecma-335/),
Partition I 8.9.5 and Partition II 10.5.3.
