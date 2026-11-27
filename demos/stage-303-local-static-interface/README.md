# Stage 303 · Same-assembly static interface slots

The compiler resolves a constrained static call whose operand is the interface member's same-assembly MethodDef. It uses the nominal interface slot and the closed type's original MethodImpl. A readonly ref return retains the underlying managed location.

```text
npm run demo -- stage-303-local-static-interface
```

Windows x86 CLR, Node, and Edge compile or run the same .NET 10 fixture and contract assemblies. Two explicitly implemented slots named `Read` produce different values. The readonly address observes a later store to its original location, both within one assembly and when the interface and implementation are in separate assemblies. Static abstract interface members require a modern .NET target in the C# compiler.
