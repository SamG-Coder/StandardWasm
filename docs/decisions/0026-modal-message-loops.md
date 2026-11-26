# 0026 · Blocking message loops and modal dialogs on the browser event loop

Status: proposed (#176). It needs Codex's APPROVE because the WinForms platform host (gate W2) will follow it.

## Context

This serves the upstream WinForms architecture recorded in [#160](https://github.com/SamG-Coder/StandardWasm/pull/160) (gate W2, the browser window and event pump). Unmodified WinForms runs a blocking `GetMessage`/`DispatchMessage` loop in `Application.Run`. `ShowDialog` and `MessageBox` run a nested modal loop inside the calling event handler's stack frame: the handler resumes only after the dialog closes, and the nested loop also dispatches work queued before it started. The browser main thread can't block. It has to return to its event loop to receive input, run timers and present frames. Compiled code needs one mechanism that keeps WinForms' dispatch order while letting the page run.

## Experiment

Stage 79 (`npm run demo -- stage-79-modal-message-loop`) has three parts:
- **Windows reference.** It records the real dispatch order with an ordinary .NET 10 WinForms program (`Oracle/`). That program posts work, then a click whose handler posts more work and calls `ShowDialog`. The dialog posts work on `Shown` and closes on a timer, and the handler then continues. The order was identical across three runs:

  `main-shown > posted-1 > click-begin > posted-after-click > posted-during-click > dialog-shown > posted-in-dialog > dialog-timer > click-after-dialog > closing > run-returned`

  Work queued *before* the click (`posted-after-click`) runs inside the click handler's modal loop. That's the reentrancy compiled code has to reproduce.
- **Wasm candidates.** `app.wat` is the same dispatch logic as Wasm, with the modal loop as a plain nested loop in the handler's frame. It imports a message queue, and the candidates differ only in how `getMessage` waits.
- **Asyncify check.** Binaryen's Asyncify transform was tested on a minimal Wasm GC module.

Results on Windows 11 (Ryzen 7 9800X3D), Node 24.19.0 and Edge 158.0.4370.33 (Playwright, headless):

| Candidate | Trace vs Windows | Host activity during the 100 ms modal | Message round trip |
| --- | --- | --- | --- |
| **J** · JS Promise Integration: `getMessage` is a `WebAssembly.Suspending` import, the app runs on the page's own thread | identical (Node, Edge) | Edge: 25 host tasks (full rate of a 4 ms interval). Node: 7 tasks | Edge 4.1 / 3.6 µs, Node 15.8 / 16.9 µs |
| **W** · dedicated worker blocking in `Atomics.wait` on a SharedArrayBuffer queue | identical (Edge) | 25 host tasks | Edge 11.6 / 11.9 µs |
| **Asyncify** (Binaryen 132) | not applicable | (none) | (none) |

(Two runs, shown as first / second.) The host-task count is the evidence that the page thread kept running during the modal loop. In both runs a 4 ms interval fired at its full rate in Edge. Animation-frame counts in headless Edge were not stable across runs (J 3 then 0, W 5 then 3) and are not used as evidence.

**Managed references across suspension** (Codex review on #179). The click handler allocates a Wasm GC struct, `{ticks, tag = 0x5eed}`, and holds it in a local of its own frame across the whole modal loop. The dialog's timer increments `ticks` through a second reference (a global) while that frame is suspended. Under J, the host also forces a garbage collection (`gc()` via `--expose-gc` / `--js-flags=--expose-gc`) inside the timer callback, while the frame is suspended. After `ShowDialog` returns, the frame's reference must be `ref.eq` to the global, keep its tag, and show `ticks = 1`.
- Node J (one forced collection while suspended): preserved.
- Edge J (one forced collection while suspended): preserved.
- Edge W: preserved.
- Mutation check: replacing the frame's reference with a fresh struct before the comparison gives `stateCheck = 0`, so the check discriminates.

This shows only that a suspended JSPI frame keeps its GC references alive, traced and identical. Full framework lowering remains separate.

Asyncify rejects the module: "Asyncify does not yet support non-number types, like references". It can't suspend code that holds Wasm GC references, and compiled .NET code always holds them.

Engine requirements as verified:
- **J:** Edge 158 has JSPI enabled. Node 24.19.0 needs `--experimental-wasm-jspi`.
- **W:** needs `crossOriginIsolated`, which in turn needs COOP/COEP headers on a secure context (`localhost` or HTTPS). A custom non-secure host such as `http://x.test` stays non-isolated even with the headers, which the stage confirmed separately.

## Decision

Use **J**: the WinForms UI thread runs on the page's main thread, and every blocking platform wait (`GetMessage`, `WaitMessage`, `MsgWaitForMultipleObjects` on the UI thread) is a JSPI suspending import. `Application.Run` and its exported entry are wrapped with `WebAssembly.promising`.

- **Nested loops.** Modal loops (`ShowDialog`, `MessageBox`, `DoEvents`, drag/drop and menu tracking) are ordinary nested loops in compiled code. The suspended Wasm stack keeps their frames, so dispatch order follows Windows without restructuring framework code.
- **Single entry rule.** While compiled code is suspended, the host never calls into Wasm directly. Input, timers, WebGPU callbacks, resize and accessibility requests become queued messages, or are answered from host-side state, and are delivered by resuming `GetMessage`. That's also WinForms' own model (everything arrives as a window message), and it rules out reentrant JS→Wasm calls.
- **Main-thread services stay put.** The DOM, input, accessibility and WebGPU stay on the main thread with the UI thread, with no proxying and no isolation requirement for single-threaded applications.

W stays available as the mechanism for **other** managed threads. A background thread can run in a worker and block for real (`Control.Invoke` from a worker, `Thread.Sleep`, `Monitor.Wait`) while the UI thread uses JSPI. It's not the UI-thread design, because it would move DOM, input and accessibility behind proxies and make cross-origin isolation mandatory for every application.

## Consequences and open items

- **Engine support is a requirement.** The browser host checks for `WebAssembly.Suspending` and reports a clear unsupported-engine diagnostic, not a degraded mode. Node-based test runs pass `--experimental-wasm-jspi` until Node enables it by default.
- **Multithreaded managed code is a separate decision.** Wasm GC objects can't be shared between workers today. Applications that touch managed objects from several threads need the shared-memory / shared-everything threading direction to be settled, or a documented single-UI-thread restriction with a diagnostic. W's measurements here only cover message passing.
- **Rendering cadence is not measured here.** Headless Edge's animation-frame delivery varied between runs, so this stage shows only that the page thread keeps running tasks during a modal loop. Frame cadence and input latency during modal loops belong to W3 measurements in a visible window.
- **Suspension cost is small next to dispatch.** About 4 µs per resumed message in Edge. Real WinForms messages carry painting and layout work orders of magnitude larger.
