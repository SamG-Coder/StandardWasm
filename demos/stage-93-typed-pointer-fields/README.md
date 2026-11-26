# Stage 93 · Typed null pointer fields

Run `npm run demo -- stage-93-typed-pointer-fields` and `npm test`.

Two ordinary .NET Standard assemblies pass null pointers through linked
fields, parameters and returns. The fixture includes signed and unsigned
primitive pointees, two distinct named structs of the same size, and a
pointer-to-pointer. Overload selection preserves both named and primitive
pointees. The default field values agree with the CLR. The demo returns 38.

The verification suite changes the test field reference to the other named
pointee and checks that both the CLR and StandardWasm reject the reference.
Changing the method reference from an `int*` to a `uint*` parameter also fails
binding on both runtimes. Other diagnostics cover a nonzero address, an
indirect read and a pointer export. See the
[pointer signature decision](../../docs/decisions/0035-typed-null-pointer-signatures.md).
