# 0019 · Resolve known instance members across DLLs

Proposed in [#130](https://github.com/SamG-Coder/StandardWasm/issues/130).

Match the calling convention, including implicit `this`, and the exact
metadata signature before binding the member in the target assembly.
Register the target method with its declaring class as the first Wasm
parameter. For `newobj` the emitter allocates the registered Wasm GC
struct and supplies that receiver. For a directly resolved `callvirt`
the emitter checks the evaluated receiver for null before making the call.

This model supports public members on the current sealed class layouts.
Calls that require virtual or interface dispatch need a separate
representation. See [ECMA-335 Partition III, sections 4.2 and 4.21](https://ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf).
