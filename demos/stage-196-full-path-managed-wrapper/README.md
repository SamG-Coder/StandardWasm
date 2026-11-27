# Stage 196 · Managed full-path wrapper

Compile a .NET 10 fixture whose SDK-generated `GetFullPathNameW` wrapper
uses the same pinning and last-error sequence as Windows CoreLib. This
lowering accepts readonly or writable UTF-16 input and requires writable
output. Managed owners stay alive while the temporary linear buffers are in
use. The compiler copies only the requested writable range back and
publishes the original system error through the saved P/Invoke slot.

Run `npm run demo -- stage-196-full-path-managed-wrapper` with the x86 .NET
10 runtime and Edge installed. The same fixture runs on Windows x86, Node
and Edge. It covers success, Unicode, sizing with and without an output
address, short buffers, errors, disjoint ranges of one managed array, and
a filename pointer into native-backed output.

The browser host uses a virtual current directory for lexical resolution.
A filename pointer may only be returned into native-backed output. A
managed output request beyond its backing array fails with error 998.
Managed scratch output is limited to 32,768 UTF-16 code units. Short-buffer
checks cover the initial NUL and the requested write boundary.
