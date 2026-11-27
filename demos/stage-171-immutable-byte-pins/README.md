# Stage 171 · Immutable byte pins

Run `npm run demo -- stage-171-immutable-byte-pins` with the .NET 10 SDK,
the x86 Windows runtime, Node, and Microsoft Edge. Results are written to
`artifacts/stage-171-immutable-byte-pins/results.json`.

A fixed `ReadOnlySpan<byte>` over an immutable PE field can lend a native
address to a proven in-bounds byte read. The compiler copies the original
field, including the terminator outside a UTF-8 literal's logical span,
into the function's shadow stack. The shadow stack unwinds with the frame.
The fixture covers both a direct dereference and reads at constant offsets.
A pointer passed to a separately compiled helper retains its original
located diagnostic because its lifetime is unproven.

The same pin supports the trusted Windows wrapper chain that passes a
NUL-terminated PCSTR to `GetProcAddress`. The scope proof checks the
export lookup signature and requires the terminator in the original field.
