# Stage 20 · Managed UTF-16 strings

From the repository root after installing dependencies:

    npm run demo -- stage-20-managed-strings
    npm run test:winforms
    npm test

The demo compiles CodeUnit from a .NET Standard DLL while the compiler runs in Wasm. Input (0, 3) returns 55357, the high surrogate in the C# rocket literal. The fixture also checks empty strings, non-ASCII BMP data, an embedded NUL, a lone surrogate and a literal with a multibyte compressed length.

The compiler reads CLI ldstr tokens from the #US heap, validates the compressed length and terminal flag, and copies only the little-endian UTF-16 data to a passive Wasm data segment. It materializes packed i16 data behind an immutable string reference. String.Length and indexed char reads operate on UTF-16 code units. Malformed heap entries produce a compiler diagnostic without touching the destination. Out-of-range access traps in the emitted module.

The counter exports the title, button caption and result prefix from the running managed module. Its resource JSON contains numeric control-schema IDs. JavaScript reads the string by code unit for drawing and accessibility. [Stage 21](../stage-21-managed-formatting/README.md) adds managed label formatting and concatenation. String interning, identity, general formatting and the wider String API remain open.

The metadata layout follows [ECMA-335, Partition II, section 24.2.4](https://www.ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf).
