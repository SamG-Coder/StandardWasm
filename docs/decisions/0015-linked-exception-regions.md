# 0015 · Linked exception regions

Proposed in [#122](https://github.com/SamG-Coder/StandardWasm/issues/122).

Exception transfers use function-local control flow and a shared Wasm tag
section when the compiler combines a bundle into one standalone module.
Admit CIL rethrow and endfinally in scalar dependency bodies. Keep the
existing scalar signature checks on linked calls and the existing
validation of handler boundaries and continuations.

The verifier uses separate entry, intermediate and leaf DLLs so both
exception propagation and malformed diagnostics exercise assembly
resolution. The reported assembly index is the DLL's position in the
input bundle.
