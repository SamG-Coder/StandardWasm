# Stage 269 · Boolean class-field byrefs

A writable Boolean field in a managed class can provide a `ref bool`.
The location keeps its original object and field selector. Reads use
the field's unsigned packed byte and writes update the same field. A
null receiver raises `NullReferenceException` when taking the address.

Run the same .NET Standard 2.0 library on Windows x86 CLR, Node and Edge
with a frozen compiler built by `npm test`.

    npm run demo -- stage-269-boolean-class-byrefs

The fixture covers inherited fields, aliases to two fields and two
instances, ordinary field reads after an aliased write, a Boolean local
in the same module, a coexisting `ref int`, and a typed null receiver.
An ordinary `ref byte` field remains a diagnostic even though the CLR
uses the same packed physical representation.

The unchanged browser Mono Blazor input compiles past the original
Boolean field address and the call to `Volatile.Write(ref bool, bool)`.
A separate runtime probe reaches the fallback for
`Unsafe.As<bool, VolatileBoolean>`, which throws on both Node and Edge.
The same exception occurs with an existing local Boolean byref, so
running the original volatile access needs a separate alias lowering.
