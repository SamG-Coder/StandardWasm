# Stage 268 · Fieldless explicit-size values

An explicit-layout struct with a declared size and no instance fields can
serve as padding in another managed type. The compiler gives it an empty
nominal Wasm GC layout and preserves the managed fields around it. Static
fields and separately boxed instances retain their normal behavior.

An exact CLI `sizeof` of the fieldless type uses its declared size. This
stage supports default packing and positive sizes through 65,535 bytes,
the CoreCLR array element limit. Existing explicit scalar overlays retain
their prior representation. Taking a byte address of the padding or
measuring an enclosing layout that needs physical padding still produces
a compiler diagnostic.

Run the same .NET Standard 2.0 library on Windows x86 CLR, Node and Edge
with a frozen compiler built by `npm test`.

    npm run demo -- stage-268-fieldless-explicit-size

The fixture verifies the exact sizes of 60-byte and 13-byte structs, a
managed copy of a padded record, nominal box type and identity, and static
field access. The CLR also measures the enclosing record and accesses the
last byte of a padding value. The compiler rejects those byte-level views.
