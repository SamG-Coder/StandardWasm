# Stage 218 · Static read-only Int32 spans

Run `npm run demo -- stage-218-static-int32-spans` with Windows x86 .NET 10,
Node, and Edge. The same ordinary assembly is used for all three runs.
Evidence is written to `artifacts/stage-218-static-int32-spans/results.json`.

A same-module `ldtoken` on an aligned and immutable PE RVA field followed by
the canonical `RuntimeHelpers.CreateSpan<int>` creates a read-only Int32
view. The existing managed span representation carries the element count
and a typed owner backed by the field bytes. This stage verifies internal
arguments and returns, signed values, little-endian decoding, and immediate
reads with the original bounds exception. Partial trailing elements
contribute zero to the span length. Misaligned data reports a compiler
diagnostic. Other element types and exposed element addresses remain
separate.

This is the original shape used by `System.Collections.HashHelpers.GetPrime`
in `System.Collections.Concurrent`. Its getter returns 72 entries from its
original 288-byte static field.
