# Stage 349 · CoreLib runtime type comparisons

CoreLib calls its own `System.Type` operators through MethodDef tokens. When the right operand comes directly from `Type.GetTypeFromHandle`, it is a canonical runtime type or null. The upstream operator can finish through reference equality and its runtime type and null checks. The compiler uses the existing descriptor comparison for this case and retains the managed operator at other direct call sites.

```sh
npm run demo -- stage-349-core-type-comparison --no-rebuild
```

The .NET Standard fixture checks the shared descriptor comparison in Windows x86 CLR, Node and Edge for matching, different and null operands. The direct MethodDef path is exercised separately by native compiler replays of the unchanged WinForms publishes, through the original `ComparerHelpers.CreateDefaultEqualityComparer`. The replay reaches the framework's generic comparer construction. Runtime execution of the original comparer method requires that dependency.
