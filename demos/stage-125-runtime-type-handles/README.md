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

`RuntimeClass` checks `Object.GetType()` on a base class and on two different
derived classes with the same field layout. It also checks the null-receiver
exception. The compiler uses linked class identity to return the same
descriptor as `typeof`. This path covers class receivers and canonical
`Type` descriptors. Constructed generic type identity and comparison with
ordinary managed `Type` objects require separate support.

See [decision 0053](../../docs/decisions/0053-runtime-type-handles.md).
