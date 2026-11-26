# Stage 79 · Modal message-loop experiment

    npm ci
    npm run demo -- stage-79-modal-message-loop

This is an experiment backing [decision 0026](../../docs/decisions/0026-modal-message-loops.md). It needs Windows, the .NET 10 SDK with the Windows Desktop runtime, and Edge.

`Oracle/` is an ordinary .NET 10 WinForms program. It records the order in which posted work, a click handler, a modal `ShowDialog`, the dialog's `Shown` event and a timer run. The runner executes it three times and requires the same order each time.

`app.wat` is the same dispatch logic as Wasm. It imports a message queue (`host-core.mjs`). Two candidates supply a blocking `getMessage`:
- **J:** JS Promise Integration on the page thread, in Node (with `--experimental-wasm-jspi`) and in Edge.
- **W:** a dedicated worker blocking in `Atomics.wait` (`worker-w.mjs`), in a cross-origin-isolated Edge page served from `localhost`.

Expected:
- All three runs print "trace matches Windows".
- Each reports host tasks (and, in Edge, animation frames) during the 100 ms modal, and a per-message round trip in microseconds.

Results are written to `artifacts/modal-message-loop/results.json`. The runner exits on its own and is not part of `npm test`.
