# Stage 246 · Assembly metadata attributes

The exact CoreLib `GetCustomAttributes<AssemblyMetadataAttribute>(Assembly)`
overload reads the selected assembly's original metadata. The compiler validates
AssemblyDef custom-attribute records, decodes the constructor's serialized
strings and constructs real CoreLib `AssemblyMetadataAttribute` instances in
their row order. The result is a typed array that uses the original CoreLib
enumerator and the normal `Key` and `Value` getters.

The same .NET Standard fixture runs on Windows x86 CLR, Node and Edge. It checks
a dependency with no matching attributes, dynamic selection of two distinct
assemblies, duplicate keys, a null value, an empty string, Unicode including a
supplementary character, fresh attribute instances and the null-assembly
exception. Other attribute types and user-defined assembly implementations
remain outside this query's scope.

Run the stage with the generated compiler.

```sh
npm run demo -- stage-246-assembly-metadata-attributes
```

To verify with genuine browser runtime dependencies, set
`STANDARDWASM_MONO_CORELIB` to the selected CoreLib PE or Webcil image. Evidence
is written to `artifacts/stage-246-assembly-metadata-attributes/results.json`.
