# Stage 10 · Portable control flow

Run this from the repository root after installing dependencies.

    npm run demo -- stage-10-portable-control-flow

The compiler running inside Wasm reads FlowLibrary.dll and emits Fold. The generated function returns 17 for (6, 20). The test also checks signed and unsigned branches, a value carried across a merge, and arithmetic at the integer boundaries.

The portable backend reads the i32 local signature from StandAloneSig metadata. It decodes short and long IL branches, checks instruction boundaries and propagates evaluation-stack depths over the reachable graph. It emits a Wasm loop dispatcher with i32 locals for the IL locals, program counter and evaluation-stack slots. Each reachable instruction has its own dispatcher arm. The design favors a straightforward verifier and can produce larger modules than basic-block lowering.

The implementation handles initialized i32 locals. Calls, switch, exception sections, and non-i32 locals remain outside this stage. The suite exercises malformed branches, mismatched stack merges, invalid local indexes, stack underflow and fallthrough past the end. It compares emitted bytes with the CLR compiler and executes the generated modules in Node and Edge.
