# Stage 76 · Initializer reachability

Run `npm run demo -- stage-76-initializer-reachability` and `npm test`.
A genuine .NET Standard executable method calls an independent type in
a dependency that also declares a type initializer. The unused type's
initializer does not prevent compilation.

Node and Edge compare the results with the CLR for plain and bundled
compilation. A reached initialization trigger and a genuine module
initializer still get an unsupported diagnostic and leave the destination
unchanged. The compiler does not run or skip required initializers. Static
field storage, precise initialization triggers, exception wrapping and
reentrancy remain tracked in [#170](https://github.com/SamG-Coder/StandardWasm/issues/170).

See [ECMA-335](https://ecma-international.org/publications-and-standards/standards/ecma-335/),
Partition I 8.9.5 and Partition II 10.5.3.
