# Stage 275 · Runtime assembly arrays

The linker gives `Assembly[]` one signature identity when an element is
named by a CoreLib definition or by a reference that resolves to that
definition. Array allocation and element loads use the same assembly
descriptor representation. A user assembly can declare the same full
type name and keeps its own nominal element and array identities.

Run the same .NET Standard 2.0 DLL on Windows x86 CLR, Node and Edge with
a frozen compiler built by `npm test`.

    npm run demo -- stage-275-runtime-assembly-arrays

The fixture checks descriptor identity through array allocation, store,
load and the default null element. A separately linked assembly provides
the user-defined type with the same name. The existing array covariance
diagnostic is checked alongside the CLR result for that operation.

The ordinary `AppDomain.GetAssemblies()` call now links its original
managed method. In the stock browser Mono fixture, compilation reaches
`AssemblyLoadContext.InternalGetLoadedAssemblies`, whose runtime body is
still unsupported. Set `STANDARDWASM_MONO_CORELIB` when the matching
browser runtime package is outside the NuGet cache.
