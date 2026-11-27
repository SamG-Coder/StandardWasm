# 0102 · Concurrent verification and grouped state dispatch

Status: proposed (#316).

`npm test` took 50–90 minutes. Only one suite can run on the shared machine at a time, so every lane queued behind it. This record covers three changes that make the suite fast while it makes the same assertions. Each change was measured on its own.

## 1. Grouped state dispatch (compiler)

`BranchWasm.Emit` lowers a method body to one `loop` that holds one `if (pc == N)` per IL state, in pc order. Every state ended with `br` back to the loop head, so each executed instruction re-tested every earlier state. A step in a large method cost thousands of comparisons. The Wasm-hosted compiler took 132 s to compile itself, against 0.2 s for the same code on the CLR.

- **Fallthrough.** When a state's constant successor is the next emitted state, the `br` is omitted and control falls into the next test. `pcLocal` is still written, so exception dispatch reads the same state as before.
- **Groups.** A body with more than 32 states puts consecutive states into blocks of about √n. Each block starts with `br_if 0 (pc >= first pc of the next group)`, so a branch back to the loop skips a whole group with one comparison. The branch to the loop leaves one extra block. A pc that matches no state still reaches the trailing `unreachable`.

Semantics are unchanged. Every generated module's bytes change. The compiler module shrinks from 2,026,278 to 1,950,182 bytes, because dropped branches outweigh the group headers. The evidence is the existing suite: C = G1 = G2 = G3 byte for byte, all CLR comparisons in Node and Edge, and every generated-code case.

## 2. Concurrent jobs (harness)

`scripts/verify-jobs.mjs` splits `verify()` into independent jobs. Edge runs them on separate browser contexts. In a release, Node also runs them on worker threads, at the same time:

- **Direct comparisons** stay grouped by module, in oracle order, on one instance per module, so static state evolves as it does on the CLR.
- **Generated-code cases** compile and instantiate a fresh module each, so they shard by case.
- **Generations.** Decision 0004 requires C = G1 = G2 = G3, where G(n+1) = G(n)(DLL). Each generation is accepted only if it equals C byte for byte. The G2 job and the G3 job therefore start at once from an instance of C's bytes. `verifyConcurrently` accepts the G2 result only after G1 is proven identical to C, and G3 only after G2. At that point the instance that ran is an instance of exactly the module the previous generation returned. B's exports still produce only G1, and every later compilation runs inside a generated module (decision 0004 item 2). Every generated-code case runs through G2's exports.

Results are combined in the sequential suite's order: CLR comparisons, then G1 = C, then G2 = G3 = G1, then the generated-code cases. The first failure in that order is rethrown with its original message. `verify()` runs the same jobs one after another for single-engine callers.

`STANDARDWASM_TEST_LANES` sets the number of jobs per engine. The default is (logical cores − 2) / 2. `--verify-only` repeats only the two verification phases over an earlier run's artifacts, for development. A gate always runs the whole suite.

**Engine stack.** Node worker threads default to a 4 MB stack. With every core busy, V8's tier-up lags, and one oracle case (`BootstrapLibrary generic base RecursiveTypeArgument`, 236 levels deep) still runs in Liftoff frames that overflow V8's default ~1 MB limit. The sequential suite passed only because tier-up had finished by then. Edge is launched with `--js-flags=--stack-size=3800`, which the renderer thread accommodates: `--liftoff-only` with that limit passes and does not crash. #318 tracks the compiler's stack depth itself.

## Engines per suite

Owner direction, 2026-11-28, quoted in [#316](https://github.com/SamG-Coder/StandardWasm/issues/316#issuecomment-7230307519): `npm test` runs for every change and every gate and verifies in Edge, the engine the product ships to. `npm run test:release` (`--release`) also runs the same jobs in Node, concurrently, and is required only for an actual release of the library. An Edge-only run gives Edge every lane. Both paths save the verified G2 compiler to `artifacts/StandardWasm.Compiler.wasm` for the stage runners.

## 3. Binary oracle store (harness)

`Verify` writes the shared assembly images once, to `artifacts/oracle.blobs.bin`. `artifacts/oracle.json` keeps the cases and a `blobLengths` index, which shrinks it from 150 MB to 23 MB. Node workers share the blob bytes through one `SharedArrayBuffer`, and the browser fetches the same file as `/blobs.bin`. `invoke.mjs` accepts `Uint8Array` byte inputs, so no worker expands the images into number arrays. `scripts/oracle-store.mjs` still reads the older inline `blobs` form.
