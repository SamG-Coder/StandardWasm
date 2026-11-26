# 0025 · Memory model for pointers into managed memory

Status: proposed (#167). **The model is a candidate, not normative**, until the memory-pressure item below is resolved. The lifetime rule and the rejection of A are proposed as normative now. It needs Codex's APPROVE because the portable compiler will follow it.

## Context

Running upstream WinForms and runtime code unmodified ([upstream WinForms decision, #160](https://github.com/SamG-Coder/StandardWasm/pull/160)) means compiling IL that takes addresses of managed memory: `fixed` over arrays, strings and array elements, pointer arithmetic, pointers handed to callees, `Span<T>` over arrays and `stackalloc`, and addresses of struct locals. Upstream WinForms uses `unsafe` in 419 of 2,233 source files. Wasm GC objects have no address, so the compiler needs a representation before it accepts pointer IL on managed memory.

## Experiment

Stage 77 (`npm run demo -- stage-77-pointer-memory-model`) has three parts:

1. **Correctness and cost.** It records CLR results for eight patterns in an ordinary netstandard2.1 library (34 cases), then runs hand-written lowerings of the same scenarios under three models in Node and Edge. The scenario text is shared, and only the array, pin and span primitives differ:
   - **A** · payloads are Wasm GC arrays. `fixed` copies the array into linear memory and copies it back at scope exit.
   - **B** · every object is in linear memory as `[length][payload]`. A reference is an address.
   - **C** · reference-holding objects stay in Wasm GC. A blittable array or string is a GC wrapper `{payload address, length}` whose payload lives in linear memory. `fixed` yields the payload address, and nothing moves.

   In every model a span is `(owner, base)`. Writes through it use only that pair.
2. **Liveness and reuse** (`liveness.wat`, model C). A `Span<byte>` over a `new byte[64]` outlives the array variable while 50 new arrays per round are allocated. A payload allocator reuses freed blocks, and payloads are freed when `FinalizationRegistry` reports their wrapper dead (30 rounds, explicit `gc()`). The `Span` either carries its owner or doesn't.
3. **Memory pressure** (`pressure.wat`, model C). This part runs with no forced collection. 64 KiB payloads are allocated and dropped, either in 200 rounds of 50 that yield to the event loop, or in one synchronous loop of 5,000.

All models share one linear shadow stack for address-taken locals and `stackalloc`, because no Wasm model can take the address of a Wasm local.

Results on Windows 11 (Ryzen 7 9800X3D), Node 24.19.0 and Edge 158.0.4370.33 (Node / Edge). Timings are medians of 5 after warm-up. Edge's `performance.now()` is coarsened to 0.1 ms on this page (not cross-origin isolated).

| Model | CLR cases | 20.97 M element reads | 20k `fixed`, 64 B | 2k `fixed`, 64 KiB | 200k `new byte[32]` |
| --- | --- | --- | --- | --- | --- |
| A | **29/34**: all 5 `FixedAliasing` cases wrong (CLR 11, A 1) | 5.6 / 4.7 ms | 0.77 / 0.7 ms | **70.7 / 56.8 ms** | 1.3 / 1.1 ms |
| B | 34/34 | 5.5 / 5.0 ms | 0.08 / 0.1 ms | 0.03 / ≈0 ms | 0.7 / 0.6 ms |
| C | 34/34 | 5.5 / 4.8 ms | 0.08 / 0.1 ms | 0.04 / ≈0 ms | 0.5 / 0.5 ms |

Repeated runs reproduced every correctness result. Element-read timings moved with machine load (Node B 5.5–9.6 ms, C 5.5–8.1 ms; Edge 4.8–5.1 ms for both). A B-versus-C access difference is not established.

| Liveness (model C) | Node | Edge |
| --- | --- | --- |
| `Span` carries its owner | `span[0]` = 90 (written 90), with 1,450 payloads freed and reused meanwhile | 90, 1,450 |
| `Span` is ownerless | **17**: the payload was freed and reused by a later array | **17**, 1,451 |

| Pressure (model C, no forced GC) | Node | Edge |
| --- | --- | --- |
| 200 yielding rounds, 625 MiB allocated | linear memory peak **109 MiB**, 9,800/10,000 freed | peak **256 MiB**, 7,200/10,000 freed |
| one synchronous loop, 312.5 MiB allocated | peak **312.5 MiB**, 0 freed until the loop returned, 4,994 within 200 ms after | peak **312.5 MiB**, 0 freed until return, 5,000 within 200 ms after |

The earlier probe also saw `FinalizationRegistry` report 20,000/20,000 wrappers dead under explicit `gc()` in both engines. That is an observation about these engines, not a delivery guarantee: the specification lets callbacks be delayed or skipped.

## Decision

**Normative now:**
- **Reject A.** The CLR makes managed and pointer accesses to a pinned array alias, and a copy breaks that. It is also linear in array size on every `fixed`.
- **Interior references carry a traced owner.** Every managed interior reference to blittable payload data carries its owner as a traced Wasm GC reference, alongside the address: `Span<T>` / `ReadOnlySpan<T>` over array or string data, byrefs from `ldelema` / `ldflda` on such data, and the `ref` fields inside spans. Copies, slices, parameters, returns, locals and fields of `ref struct`s all carry the pair. Element access uses only the address. A `stackalloc` span has a null owner and points into the shadow stack, which is valid for the frame.
- **Raw pointers stay valid only while pinned.** Unmanaged pointers (`T*` from `fixed` or `GCHandle.Pinned`) carry no owner and are valid only while pinned, as in the CLR. The compiler keeps the pinned local's owner live until the end of the `fixed` scope on every exit path, including exceptional unwinding.

**Leading candidate, not yet normative: C**, the hybrid model:
- Wasm GC for every reference-holding object.
- Linear-memory payloads for blittable arrays and strings, reached through a GC wrapper.
- `ldelem`/`stelem` through the payload address after a bounds check against the wrapper's length.
- `fixed` yields the payload address.

It matches the CLR on every case, costs nothing to pin, and keeps references in Wasm GC.

C does not become normative until the memory-pressure strategy below is demonstrated. The JS engine's collector doesn't see linear payload memory, so dead payloads are reclaimed late (Edge kept 256 MiB of linear memory for 625 MiB of short-lived arrays even while yielding). A synchronous loop reclaims nothing until it returns, and linear memory never shrinks.

B remains the fallback if no strategy works. It is correct and as fast on these probes, but it gives up Wasm GC and needs its own precise collector and JS-reference handling.

## Open items (required before C is normative)

- **Memory-pressure strategy, with a fixture.** This was measured in stage 88 (#196): `npm run demo -- stage-88-payload-pressure`, with no forced GC, in Node 24.19.0 and Edge 158.0.4370.33. It reports peak linear memory for 625 MiB churned in yielding rounds, and for 312.5 MiB in one synchronous call:

  | Strategy | Node yielding | Node sync | Edge yielding | Edge sync |
  | --- | --- | --- | --- | --- |
  | baseline | 94 MiB | 312.5 | 256 | 312.5 |
  | ballast 1/8 (GC byte array of payload/8 on each wrapper) | 91 | 312.5 | **9.4** | 312.5 |
  | ballast 1/32 | 97 | 312.5 | 50 | 312.5 |
  | budget 32 MiB (JSPI yield in the allocation slow path, decision 0026) | 79 | 157 | 273 | 229 |
  | ballast 1/32 + budget 32 MiB | 85 | 157 | 110 | **110** |

  - **No strategy enforces a hard bound.** Nearly every slow-path yield found the budget still exceeded, because the engine had not collected yet, and browsers expose no API to request collection or to report external memory.
  - **Ballast** makes Edge collect promptly for code that yields (256 → 9.4 MiB at 1/8). It had no effect in Node, and none for a synchronous loop, because finalization callbacks only run between tasks.
  - **Budget plus JSPI yield** is the only measure that lowers synchronous-loop peaks, and it does so in both engines.
  - **Combined,** they give the lowest worst case (110 MiB in Edge for both patterns, 157 MiB in Node), but the bound is set by the engine's GC schedule, not by StandardWasm.

  **Model B measured on the same churn** (`pressure-b.wat`). Every object lives in linear memory, roots sit on a shadow stack, and a precise mark-sweep collector runs synchronously in the allocation slow path once 32 MiB has been allocated since the last collection. A 10,000-node list stays live and is traced by every collection.

  | B | Node yielding | Node sync | Edge yielding | Edge sync |
  | --- | --- | --- | --- | --- |
  | no collection | 626.5 MiB | 313.9 | 626.5 | 313.9 |
  | collect every 32 MiB, 10k live nodes | **40.4 MiB**, 19 collections, max pause ≤ 1.4 ms | **40.4**, 9 collections | **40.4**, max pause 0.1 ms | **40.4** |
  | collect every 32 MiB, **1 M live nodes** | **63.1 MiB**, max pause 4.9 ms, 35 ms total | **63.1**, max pause 1.6 ms, 13 ms total | **63.1**, max pause 2.1 ms, 34 ms total | **63.1**, max pause 1.7 ms |

  The peaks include the 8 MiB object table, which was enlarged for the 1 M run (an earlier 1 MiB table gave 33.4 MiB with 10k nodes).

  The live list was intact after every collection. Edge pause figures are at the page's 0.1 ms timer resolution.

  **Consequence.** On memory, B is clearly stronger. It enforces the budget exactly (live data + budget), even inside one synchronous call, where C peaked at 110–157 MiB at best. Its collections were cheap here. The comparison is not finished, though, because this probe flatters B in two ways:
  - **Live heap.** With 1,000,000 live list nodes, the bound still held (63 MiB) and the worst pause was 1.6–4.9 ms. That's about 2–5 ns per live object for mark plus sweep, on a simple singly linked graph. Real object graphs, with more pointers per object and less locality, will cost more per object. This is evidence that pauses scale acceptably, not a final figure.
  - Every reference held by JavaScript (DOM handles, callbacks, WebGPU objects) must become an explicit root or handle table under B. Under C, Wasm GC handles cycles through JS for free.

  B also needs precise stack maps for every compiled frame (shadow-stack spilling of references at every call), and that costs code size and speed in all compiled code, not just allocation. **Frame cost of B's roots** (`spill.wat`). This is a call-heavy probe: a recursive sum over a 262,143-node binary tree, 20 traversals, median of 7, in three forms:
- C: Wasm GC structs, with references in Wasm locals
- unrooted: linear nodes with no rooting, a lower bound that is unsafe under a collector
- B: linear nodes with every reference that is live across a call stored to a shadow-stack slot and cleared on return

| | C (Wasm GC) | linear, unrooted | B (spilled roots) |
| --- | --- | --- | --- |
| Node | 12.0 ms | 8.7 ms | 11.1 ms |
| Edge | 8.2 ms | 6.2 ms | 10.3 ms |

Rooting costs B 28% (Node) to 66% (Edge) over unrooted code, which puts B within −7% to +26% of C. Frame cost is therefore not what decides C versus B. One microbenchmark can't establish more than that.

**Where this leaves the choice.**
- **B** gives a hard memory bound and synchronous collection, at the cost of its own collector, stack-slot rooting in every frame, and explicit handle tables for every JS-held reference.
- **C** keeps engine-managed references and free cycles through JS, but only a soft bound that depends on the engine's GC schedule (the best mitigation, ballast plus a JSPI-budgeted yield, reached 85–157 MiB here).

Both are viable, and this decision now needs Codex's judgment on which property matters more for unmodified WinForms applications, rather than another probe. Then C vs B is decided with Codex.

- **Allocator.** A size-class free-list allocator with zero-initialization (the CLR guarantees zeroed arrays) and growth via `memory.grow`. The prototypes use bump and single-size LIFO allocators.
- **Interior pointers into class fields.** Pinning a field of a class instance (`fixed (int* p = &obj.Field)`) isn't covered, because class instances stay in Wasm GC. Whole-program compilation could find every type whose fields are ever pinned and give those types C-style storage. That needs its own fixture.
- **Diagnostics until implemented.** IL that pins or takes the address of managed memory remains an explicit unsupported diagnostic at the method and IL offset.
