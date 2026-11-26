# 0003 · Portable compiler diagnostic location contract

Keep the existing compiler exports and negative status codes. Add detailed variants with a caller-owned, fixed-width array of three i32 values. The result still reports a failure status or the number of Wasm bytes written. The array reports where compilation failed.

The fields are bundle index, MethodDef row and IL byte offset. The primary DLL is index 0. Use -1 for an unavailable bundle index, 0 for an unavailable row and -1 for an unavailable offset. MethodDef rows are scoped to the selected DLL. On success, clear all three location fields to their unavailable values.

Resolve errors at the call site. After a dependency method enters the compiler work queue, later decode and stack errors identify that dependency method. Metadata and signature validation can report an assembly or method with no IL offset. Scope the record to the current compilation and allocate it in the compiler's own Wasm instance, using the same GC transport approach as the byte buffers. Require no host imports or shared global diagnostic state.

The [Stage 27 fixture](../../demos/stage-27-portable-diagnostics/README.md) exercises the contract and shows how to read the fields.
