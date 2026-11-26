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

The catalog keeps each resource's UTF-8 name and payload range separate.
This supports ordinal lookup before any payload is copied and preserves
empty embedded resources. The original WinForms x86 DLL contains 199
embedded resources totaling 3,170,595 bytes. Resource discovery therefore
does not put a second copy of every resource into the generated module.
The standalone compiler exposes `FindEmbeddedResource` for the host to
obtain a validated offset and length in the original image. The export
compares the supplied UTF-8 bytes exactly and leaves resource ownership
with the caller. For the Windows CoreCLR contract, the native adapter
must apply the null-terminated UTF-16 name rules before this lookup.
The x86 CLR resolves a valid name followed by U+0000 and a suffix to
the original resource. A leading U+0000 raises ArgumentException.

Generated resource imports use the `standardwasm:resources` module.
Its `memory` import supplies the instance's wasm32 linear memory.
`reserveName` takes a UTF-16 code-unit count and returns a scratch
address. `locate` takes the bundle owner, scratch address and code-unit
count and returns the byte length and stable address packed into an
i64. Negative results identify a lookup diagnostic. The compiler keeps
console function indices stable when both import groups are present,
and the memory entry does not occupy a function index.
The executable and console loaders supply the resource imports together
when requested. The original images stay in bundle order so the owner
index identifies the same assembly at compile time and runtime. The host
appends pages for its scratch space and resource bytes. It keeps each
published resource address stable until its Wasm instance is released.
At the lookup boundary, -8 denotes a null name and -9 denotes an empty
name after applying Windows termination rules. The managed binding must
translate these results to the corresponding CLR exceptions.

The linker binds the platform `Assembly.GetManifestResourceStream(string)`
call to the verified `RuntimeAssembly` override in the selected CoreLib.
The method takes the canonical Assembly descriptor as its receiver and
keeps the original Stream result type. An unsupported instruction from
that override is reported with its original CoreLib location.

[CoreCLR 10.0.10](https://github.com/dotnet/runtime/blob/v10.0.10/src/coreclr/System.Private.CoreLib/src/System/Reflection/RuntimeAssembly.cs)
gets the payload through its native resource lookup and returns a read-only
manifest stream. Keep the embedded image alive while a stream owns its
bytes. The CLR oracle covers independent stream positions, seek, end of
stream, CopyTo, disposal, missing resources and argument exceptions.
[WinForms 10.0.10](https://github.com/dotnet/winforms/blob/v10.0.10/src/System.Windows.Forms.Primitives/src/System/Windows/Forms/Internals/ThemingScope.cs)
consumes the manifest through CopyTo before creating the Windows activation
context. Preserve that managed call path as the platform adapter develops.

The loader envelope and embedded reader are implemented. Type.Module, Type.Assembly,
Module.Assembly and Module.Name lower against the canonical descriptors. Resource
stream behavior and full-stage verification are still required.
