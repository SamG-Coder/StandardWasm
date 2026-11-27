# 0095 · C# fixed buffers in native layouts

Stage 340 of #274 applies the trusted native fixed-buffer proof from Stage 300
when binding the upstream WinForms paint methods. This extends [decision 0071](0071-struct-pointers-and-sizeof.md).

## Context

The original WinForms `BeginPaint` and `EndPaint` declarations take
`Windows.Win32.Graphics.Gdi.PAINTSTRUCT*`. Its last field, `rgbReserved`, is
`Windows.Win32.__byte_32`, which wraps `fixed byte Value[32]`. Roslyn emits a
sequential nested value type with one `FixedElementField` and records the
storage extent in `ClassLayout.Size`.

## Decision

Keep the existing `FixedBufferMetadata` proof for native layout. The declaring
field's authentic `FixedBufferAttribute` supplies the primitive element type
and count. The compiler verifies the matching nested storage field, packing
and declared size. The native alignment comes from the element type. Other
explicit sizes still produce a diagnostic, including the plain `Size = 12`
structure in Stage 143.

Stage 340 verifies the 64-byte x86 `PAINTSTRUCT` with `rgbReserved` at offset 32,
the sizes of a five-byte buffer and a 56-byte mixed structure, scalar field
addresses and writes, and a fixed-buffer element read through a native pointer.
The tests compare Windows x86 CLR, Node and Edge. The compiler also checks the
paint declaration's complete pointer shape before emitting its host import.

Stage 341 separately verifies managed fixed-buffer storage and aggregate copy.
Stage 340 retains the located diagnostic for its direct-indexing managed-local
case.
