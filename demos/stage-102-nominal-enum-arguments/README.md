# Stage 102 · Nominal enum arguments

A linked .NET Standard library declares closed slots for two distinct enums with the same signed byte storage. A third slot in the verifier uses the underlying primitive. The compiler preserves the enum TypeDef for signature matching and uses the underlying storage for reads and writes.

Run `npm run demo -- stage-102-nominal-enum-arguments`. It returns 11 after checking the default value and copying packed enum fields. The full suite covers sign extension, boundary values, and two linked field signatures that the CLR rejects.

A direct vector of enum values still requires nominal array binding and lowering.
