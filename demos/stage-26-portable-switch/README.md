# Stage 26 · Portable switch dispatch

Run from the repository root.

    npm run demo -- stage-26-portable-switch

The compiler running in Wasm reads SwitchLibrary.dll and emits a standalone module. Its Classify export returns 17 for input -3.

The compiler decodes the IL jump table and checks each destination is an instruction boundary. Its stack analysis consumes the i32 selector and carries the remaining stack types into every case and the default. Wasm br_table interprets the selector as unsigned, so negative and out-of-range indices use the default destination. Each case sets the next IL offset for the existing dispatcher.

Run npm test to compare the same .NET Standard DLLs on the CLR, Node and Edge. The fixture includes a dense switch with a gap and shared destinations, a state machine with goto case, and a switch expression with a wide accumulator. The oracle also runs two valid IL variants on the CLR to cover a backward table entry and an i64 value remaining on the evaluation stack across every case. Malformed table counts and destinations are rejected without writing output bytes. Each generated-code case is replayed with the self-recompiled compiler.
