# 0046 · Reuse assembly images within a compiler invocation

A packed application contains a length-prefixed entry image followed by its
dependency images. The portable compiler validates the image ranges and
materializes one bounded byte array per owner. Metadata, signatures, layouts
and IL all read that owner's array. The console-import retry uses the same
images as its first pass.

The image collection belongs to one public compile call. The next call builds
a fresh collection from its input. This keeps owner indexes stable throughout
compilation and prevents reads from crossing between assemblies. Standalone
library compilation retains its existing linking behavior.
