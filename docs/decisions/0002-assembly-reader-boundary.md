# 0002 · Byte-buffer boundary for assembly discovery

Status: proposed. The native CLI currently hands a path to Mono.Cecil. Move filesystem access to the host and let the eventual compiler core receive the complete file image in an immutable-for-the-call buffer. The first portable reader should locate the CLR header and metadata root. Parsing metadata tables, resolving assemblies and decoding IL are separate increments.

## Core interface

Use an ordinary .NET Standard byte array inside the managed reader. Take a caller-supplied result array to avoid depending on user-defined value types or by-reference parameter lowering at this stage.

```csharp
static int LocateMetadata(byte[] image, int[] location)
```

The caller supplies non-null arrays, with at least two slots in location. On success return 0, set location[0] to the file offset of the metadata root, and set location[1] to its declared size. Leave location unchanged on failure. Return 1 for invalid or truncated structures, 2 for unsupported layouts, and 3 for an otherwise supported PE image without a CLI directory. The host maps these codes to diagnostics. This interface reports discovery only. Success carries no promise about the validity of metadata tables, IL or the rest of the PE.

Treat integer fields as unsigned little-endian values. Establish a bounded range with offset <= limit and size <= limit - offset before forming its end. For subranges, use the enclosing record's limit, such as the optional header's end. Perform PE arithmetic on uint and narrow to signed array indexes only after the result fits within image.Length. Use the same range check for helper reads and RVA conversion. Keep filesystem calls, Cecil and Binaryen out of the portable reader.

## Discovery sequence

1. Check the DOS header and MZ signature, read e_lfanew, and bound the PE signature and COFF header. Use COFF's SizeOfOptionalHeader to find the section table and validate the span of its 40-byte entries.
2. Read the optional header magic. PE32 uses offsets 92 and 96 for the directory count and first entry, and PE32+ uses 108 and 112. Check that the count includes directory 14 before reading its RVA and length. A missing entry or an all-zero pair reports no CLI directory. A half-zero pair is malformed.
3. Resolve the complete CLI directory through the section table. Find a section containing the starting RVA in its loaded extent, using the larger of its virtual and raw sizes. Reject ambiguous section mappings and cap each accepted range by both raw size and the file size. Reject raw-less virtual tails and ranges that cross a section boundary. The first increment supports section-backed directories. Header-mapped directories can be added with their own fixture.
4. Require a CLI directory of at least 72 bytes and validate its cb field against the bounded directory. Read metadata RVA and size at offsets 8 and 12 of the CLI header. Resolve the complete metadata range with the same section rules and check the BSJB signature. Return only after all discovery checks pass. A later stage reads stream offsets relative to this returned range and validates each against its declared size.

For example, a section at RVA 0x2000 with raw offset 0x200, raw size 0x100 and virtual size 0x180 maps RVA 0x2080 and length 0x20 to file range [0x280, 0x2a0). RVA 0x2100 lies in the virtual tail and has no corresponding file bytes. A range starting at 0x20f0 with length 0x20 also fails.

## Acceptance for implementation

- Compare returned metadata offsets and sizes with System.Reflection.PortableExecutable.PEReader on Sample.dll, ArrayLibrary.dll and a PE32+ managed fixture.
- Exercise truncated headers, optional-header lengths that exclude directory 14, zero and half-zero directory pairs, overlapping RVA mappings, virtual-tail ranges, raw spans beyond EOF, overflow-sized ranges and a bad metadata signature. Check statuses and sentinel result slots on failure.
- Add a bytes fixture covering unsigned array reads, writes, and u8 truncation before compiling the reader. A Wasm GC array with packed i8 elements and unsigned reads can represent the byte array compactly.
- Compare the same reader assembly on the CLR, Node and Chromium. Populate the Wasm byte array through fixture exports in the first demonstration. Choose the production host transfer ABI separately, based on measured copy and call overhead. Keep the current Cecil path available until the portable reader can supply all metadata the lowering phase uses.

The PE fields are described in [Microsoft's PE format specification](https://learn.microsoft.com/en-us/windows/win32/debug/pe-format). The CLI and metadata layouts are specified in [ECMA-335, Partition II, sections 24 and 25](https://www.ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf).

## Feasibility check

A local discovery probe and .NET PEReader agreed on the Release fixtures. Sample.dll returned file offset 720 and size 1524. ArrayLibrary.dll returned file offset 668 and size 1268. The probe rejected four mutations of each fixture: a DOS header missing the final e_lfanew byte, a file truncated one byte before the metadata range ends, a corrupted BSJB signature, and a metadata length of 0xffffffff. These offsets are observations from these builds. Compare against PEReader dynamically in the implementation tests.
