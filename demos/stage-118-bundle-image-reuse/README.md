# Stage 118 · Bundle image reuse

Run `npm test` to build the self-hosted compiler. Then run
`npm run demo -- stage-118-bundle-image-reuse` on Windows with the .NET 10 SDK,
Windows Desktop runtime, Node and Microsoft Edge.

The runner uses the original [Stage 72 WinForms application](../stage-72-winforms-windows-reference/README.md)
and the [Stage 95 browser drop and console comparisons](../stage-95-managed-companions/README.md).
It checks that Node and Edge agree on the original WinForms compilation
diagnostic under the existing browser deadline. On this milestone it reaches
the `EventHandler` closure after constructing `System.Drawing.Size`.
The WinForms window opens only in the native Windows reference run.

Results under `artifacts/bundle-image-reuse/` include the compiler and bundle
hashes, image count and bytes, original diagnostic, and elapsed launch time
for Node and Edge. Time includes host setup and compilation. A fully unpacked
bundle copies each image once. The compiler keeps those images for the
invocation, including any repeat to reserve console imports. The tests also
cover truncated or empty image ranges and changing an assembly in the same
caller buffer between compiler invocations.

See the [bundle image reuse decision](../../docs/decisions/0046-bundle-image-reuse.md).
