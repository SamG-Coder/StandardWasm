# Stage 175 · Copy to sliced span fields

Build an ordinary .NET Standard 2.1 library that copies a readonly character span
into a writable view sliced from a ref struct field. The compiler evaluates
the receiver, field and slice arguments in the original order. It uses the
existing overlap-safe copy and preserves typed failures from Slice and CopyTo.

Run `npm run demo -- stage-175-character-span-field-copy`. The same DLL returns
401, 403, 407 and 409 on the Windows x86 CLR, Node and Edge.
