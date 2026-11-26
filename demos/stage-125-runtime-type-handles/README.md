# Stage 125 · Runtime type handles

Run `npm run demo -- stage-125-runtime-type-handles`. For input 5,
the compiled kernel compares a TypeRef in the main assembly with a
TypeDef in the dependency and returns 1.

`npm test` compares the original managed fixtures on the CLR, Node and
Edge. Cases cover repeated references, identically named types in different
assemblies, a default runtime handle, and a type with a throwing initializer.
The host comparison checks that a numeric object never aliases a Type.
The compiler reports unsupported constructed type tokens and missing
dependencies at their IL offsets. The verifier checks exact facade scopes.
The standalone checks also compile one DLL through both compiler entry points.
Boxing a runtime handle or casting to a reflection type still produces a
diagnostic.

See [decision 0053](../../docs/decisions/0053-runtime-type-handles.md).
