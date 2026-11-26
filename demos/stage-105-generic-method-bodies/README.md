# Stage 105 · Generic method bodies

Run `npm run demo -- stage-105-generic-method-bodies` and `npm test`.
`GenericBodyDefinition.dll` compiles one `Holder<T>` definition. The app and
`GenericBodyConsumer.dll` construct and call `Holder<int>`, `Holder<uint>`,
`Holder<sbyte>`, `Holder<byte>` and `Holder<string>` directly. The compiler
emits a separate constructor and method body per closed declaring type. It
substitutes the type argument into parameters, returns, locals and field
operands. The demo returns -122103 for `Run(-128)`.

`Holder<int>` and `Holder<uint>` share i32 storage. The verifier compares
signed and unsigned division and ordering over boundary operands, including
`int.MinValue / -1` overflow and division by zero. The verifier also covers
`sbyte` and `byte` packed storage and the null `string` instantiation. Holders
pass between the app and the consumer DLL. Calls cover instance, static,
private same-definition, constructor-inside-body and local-of-type-`T` paths.
The verifier compares every case with the CLR in Node and Edge.

Direct calls to a closed nonvirtual generic instance method (`Echo<U>`)
compile on reference receivers. The verifier checks that `callvirt`
evaluates the arguments before raising `NullReferenceException` on a null
receiver. The virtual member `Describe` in this fixture still reports -2 at
the calling instruction. Generic value-type declaring types and static
fields on generic types need separate work.

See the [closed generic method body decision](../../docs/decisions/0037-closed-generic-method-bodies.md).
