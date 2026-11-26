# Stage 58 · Linked arrays and strings

Run `npm run demo -- stage-58-linked-references`. Calling `Alias(0, 7)`
returns `797` after two separately built libraries write through the same
array that the entry DLL allocated.

The three .NET Standard assemblies exercise primitive array allocation,
mutation and literals, signed and unsigned overloads, string construction,
and UTF-16 results crossing assembly boundaries. The suite compares the same
DLLs on the CLR, Node and Edge. It verifies typed failures, byte equality
under dependency reordering, and generated compiler replay.

Static linked signatures admit scalar values, supported primitive arrays
and strings. User-defined reference types and interface dispatch across
DLLs remain open.
