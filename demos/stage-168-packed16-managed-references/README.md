# Stage 168 · Packed 16-bit managed references

Run `npm run demo -- stage-168-packed16-managed-references` with the .NET 10
SDK, the x86 Windows runtime, Node, and Microsoft Edge. Results are written
to `artifacts/stage-168-packed16-managed-references/results.json`.

The original fixture uses `Unsafe.As<char, short>(ref char)` and the reverse
conversion. Both views retain the same array location or linear-memory
address. Signed loads and generic `ldobj` sign-extend the same 16 bits.
Writes change the original storage and leave adjacent characters alone.
A null alias stays null and throws the CLR's typed exception when read.
