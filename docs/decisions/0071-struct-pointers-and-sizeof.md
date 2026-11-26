# 0071 · Struct pointers and sizeof

Status is proposed in #275. It builds on decision 0060 (primitive pointers in
memory 0) and decision 0065 (stackalloc). The boundary with Codex's traced
managed locations (#271, #273) was agreed in handoff 040/095. Field access
whose receiver on the stack is a native address belongs here. Any `ldloca`,
`ldflda` or `ldelema` of a managed location stays a traced byref.

## sizeof

IL `sizeof` of a value type is the 32-bit CLR's managed size. wasm32 shares
its 4-byte native width.
- **Measurement.** The compiler reads each instance field's signature element.
  The value-type registry can't be used, because it stores `bool` as int32.
  Sizes and alignments:
  - `bool`, `sbyte`, `byte`: 1
  - `char`, `short`, `ushort`: 2
  - `int`, `uint`, `float`: 4
  - `long`, `ulong`, `double`: 8, aligned to 8 even on x86
  - `nint`, `nuint`, pointers: 4
- **Nested structs.** They are measured recursively, across bundled
  assemblies, and align to their largest field.
- **Layout rule.** Each field is placed at its natural alignment. The size is
  rounded up to the largest alignment, and an empty struct has size 1.
- **Enums.** An enum has auto layout and a single `value__` field, and takes
  its underlying size.
- **Still diagnosed:** other auto-layout and explicit-layout structs, `Pack` and
  `Size`, structs with reference fields, `sizeof(T)` over a generic parameter,
  and structs with a layout-changing attribute. `[InlineArray(n)]` repeats its
  declared field n times, and `IsByRefLike` structs hold byrefs. Their declared
  field rows don't give the size. Once Stage 138 (#292) lands, an inline array
  measures as n times its element.
- **Ground truth.** The x86 CLR 10.0.10: `{byte, long}` = 16, `{byte, void*,
  byte*}` = 12 (24 on x64), `{char, bool}` = 4, empty = 1.

## Struct pointers

- **Representation.** A single-level pointer to a measurable struct is an i32
  address into memory 0, like the primitive and `void*` pointers. Other
  pointers to named structs (auto, explicit or non-blittable) keep the
  null-only storage of decision 0035.
- **Field access.** The field's offset comes from the same measurement.
  - `ldfld` through an `S*` loads at that offset, and `stfld` stores there.
    Both test the address for 0 and raise the typed `NullReferenceException`.
    The memarg carries the field offset.
  - `ldflda` through an `S*` (C# `&p->F`) adds the offset. It doesn't
    dereference, so it never throws, as on the CLR.
  - `p[i].F` is `sizeof` scaling followed by the field access.
- **Memory import.** A module imports the lent memory only when a reachable
  field access has a native receiver after stack analysis.

## Still diagnosed

- whole struct values copied through a pointer (`*p = s`, `s = *p`)
- linked (cross-assembly) struct fields through a pointer
- `fixed` and pinning (decision 0025)
- `calli`
