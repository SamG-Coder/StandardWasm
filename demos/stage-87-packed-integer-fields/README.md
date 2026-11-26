# Stage 87 · Packed integer instance fields

Run `npm run demo -- stage-87-packed-integer-fields` and `npm test`.
The two assemblies use signed and unsigned byte and short fields, character,
Boolean, and a full-width unsigned word. The derived layout includes its base
fields. The tests compare signedness, truncation, boundary values, and managed
null failures against the CLR.

Wasm GC stores narrow fields as packed i8 or i16 cells. Loads choose signed or
unsigned extension to the CLI int32 stack type. Linked field signatures still
match the exact CLI element type even when Wasm shares the storage encoding.
The compiler rejects an incompatible store at its IL offset.

See the [packed-field storage decision](../../docs/decisions/0030-packed-integer-fields.md).
