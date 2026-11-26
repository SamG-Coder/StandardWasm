# 0059 · Runtime reflection for bundled modules

Proposed in #288. WinForms startup reads the original runtime Type's
Module, uses its Name to look up a Windows module, and on the other path
opens an embedded manifest through Module.Assembly. Compile the actual
framework calls against the original module identities and resources.

## Module identity and load origins

Stage 125 Type descriptors identify a bound module and TypeDef. Reflection
will resolve the module slot to a canonical Module descriptor and its
defining Assembly descriptor. An alias for a type in one module resolves
to the same Module object as other types defined there. Names never stand
in for identity.

CLR 10.0.10 gives Module.Name the file basename for a path-loaded image.
It gives a stream-loaded image or a normal single-file bundled image
`<Unknown>`. The module row in metadata provides Module.ScopeName in
both cases. Use the basename selected by the intake for a file-backed image.
Retain bytes-only origins for embedded and Webcil images.

Older packed bundles contain repeated little-endian uint32 image lengths
and their payloads. The optional module-origin format starts with a zero
length and `SMO1`, then repeats:

| Field | Encoding |
| --- | --- |
| Image length | little-endian uint32, greater than zero |
| Basename byte length | little-endian uint32, zero for a bytes-only module |
| Image | original managed bytes |
| Basename | UTF-16 little-endian Windows filename, without a directory |

The linker validates the complete sequence before copying any payload. A
legacy bundle has no file-backed names. An intake can opt in to sending the
new format when the compiler advertises support. The original package
configuration and the managed image bytes are independent of this change.

## Embedded resources

Locate a resource by its ordinal UTF-8 ManifestResource name and prove its
range inside the declaring assembly's file-backed CLI resource directory.
PE and raw Webcil use their existing RVA mapping. Support a resource
embedded in its declaring assembly. Linked resource files and forwarded
resources remain explicit diagnostics. The reader exposes the validated
bytes for the runtime stream to consume.

The loader envelope and embedded reader are implemented. Runtime descriptor
dispatch, stream behavior, and the unchanged WinForms startup replay are
still required for Stage 131.
