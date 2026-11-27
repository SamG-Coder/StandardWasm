# Stage 183 · Computed span slice arguments

Use integer expressions for offsets and lengths on readonly spans, writable
spans, and writable span fields. The original managed expression and evaluation
order are preserved. The compiler snapshots the view only when its verified
consumer can read a descriptor.

Run `npm run demo -- stage-183-computed-span-slices`. The same .NET Standard
2.1 DLL returns 509, 521, 523 and 541 on the Windows x86 CLR, Node and Edge.
