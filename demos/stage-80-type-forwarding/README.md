# Stage 80 · CLR type forwarding

Run `npm run demo -- stage-80-type-forwarding` and `npm test`. The caller is
compiled against the original signed assembly. The replacement assembly has
the same identity and uses ordinary C# type forwarders. The caller is unchanged.

The linker follows top-level ExportedType forwarders to the final TypeDef.
Constructors, methods, fields and class signatures share that resolution.
The fixture compares with the CLR loading the same forwarding deployment.
It checks byte stability when dependencies are reordered and rejects missing
or ambiguous destinations. Nested types and multi-module exports remain open.

See [Microsoft's type forwarding documentation](https://learn.microsoft.com/dotnet/standard/assembly/type-forwarding).
