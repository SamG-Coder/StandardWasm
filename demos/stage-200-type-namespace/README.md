# Stage 200 · Runtime type namespaces

Run `npm run demo -- stage-200-type-namespace` with the Windows x86 .NET 10
runtime, Node, and Edge. Set `STANDARDWASM_COMPILER_PATH` to verify a frozen
compiler. The verifier compiles ordinary .NET Standard 2.0 assemblies and
writes evidence to `artifacts/stage-200-type-namespace/results.json`.

The compiler reads `Type.Namespace` from canonical runtime type descriptors.
Arrays take their element's namespace, constructed types take their defining
type's namespace, and nested types use the outermost definition. The helper
decodes the raw metadata text and returns null for a global type. It caches
non-null strings separately from the existing simple and display names.

The verifier covers Unicode metadata, global and nested types, linked
definitions, enum and jagged vectors, open and constructed generics, primitive
and standard reference types, and descriptors selected by `GetType()` at
runtime. It checks both simple name and namespace on one descriptor and checks
for a typed null receiver exception. Type shapes without a supported runtime
descriptor still produce compiler diagnostics.

The compiler also recognizes the original CoreLib virtual MethodDef calls to
`Type.Namespace` and `MemberInfo.Name` on canonical Type receivers. These
bindings preserve the managed `ResourceManager(Type)` constructor and let the
unchanged WinForms bundle compile further. This stage introduces no host import.

[CoreCLR namespace resolution](https://raw.githubusercontent.com/dotnet/runtime/v10.0.10/src/coreclr/System.Private.CoreLib/src/System/RuntimeType.CoreCLR.cs#L1397-L1413) ·
[ResourceManager constructor](https://raw.githubusercontent.com/dotnet/runtime/v10.0.10/src/libraries/System.Private.CoreLib/src/System/Resources/ResourceManager.cs#L203-L217)
