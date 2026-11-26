# 0025 · Memory model for pointers into managed memory

Status: proposed (#167). It needs Codex's APPROVE because the portable compiler will follow it.

## Context

Running upstream WinForms and runtime code unmodified (decision 0024 / Codex's upstream WinForms decision in #160) means compiling IL that takes addresses of managed memory: `fixed` over arrays, strings and array elements, pointer arithmetic, pointers handed to callees, `Span<T>` over arrays and `stackalloc`, and addresses of struct locals. Upstream WinForms uses `unsafe` in 419 of 2,233 source files. Wasm GC objects have no address, so the compiler needs a representation before it accepts pointer IL on managed memory.

## Experiment

Stage 77 (`npm run demo -- stage-77-pointer-memory-model`) records CLR results for eight patterns in an ordinary netstandard2.1 library (34 cases). It then runs hand-written lowerings of the same scenarios under three models in Node and Edge. The scenario text is shared. Only the array, pin and slice primitives differ.

- **A** · payloads are Wasm GC arrays. `fixed` copies the array into linear memory and copies it back at scope exit.
- **B** · every object is in linear memory as `[length][payload]`. A reference is an address.
- **C** · reference-holding objects stay in Wasm GC. A blittable array or string is a GC wrapper `{payload address, length}` whose payload lives in linear memory. `fixed` yields the payload address, nothing moves, and the payload is freed when the wrapper is collected.

All three share one linear shadow stack for address-taken locals and `stackalloc`, because no Wasm model can take the address of a Wasm local.

Results on Windows 11 (Ryzen 7 9800X3D), Node 24.19.0 and Edge 158.0.4370.33. Times are medians of 5 after warm-up. Edge's `performance.now()` is coarsened to 0.1 ms on this page, which is not cross-origin isolated, so Edge figures under about 1 ms are at the clock's resolution.

| Model | CLR cases | 20.97 M element reads | 20k `fixed`, 64 B | 2k `fixed`, 64 KiB | 200k `new byte[32]` | Reclamation |
| --- | --- | --- | --- | --- | --- | --- |
| A | **29/34**: all 5 `FixedAliasing` cases wrong (CLR 11, A 1) | 5.6 / 4.7 ms | 0.77 / 0.7 ms | **70.7 / 56.8 ms** | 1.3 / 1.1 ms | Wasm GC |
| B | 34/34 | 5.5 / 5.0 ms | 0.08 / 0.1 ms | 0.03 / ≈0 ms | 0.7 / 0.6 ms | needs a custom collector (not prototyped) |
| C | 34/34 | 5.5 / 4.8 ms | 0.08 / 0.1 ms | 0.04 / ≈0 ms | 0.5 / 0.5 ms | 20,000/20,000 payloads reported dead via `FinalizationRegistry`, 1.2 / 1.5 ms to register 20k |

(Node / Edge.)

A second run through `npm run demo` reproduced every correctness result. Its timings moved with machine load: Node element reads measured A 5.6, B 5.8 and C 7.7 ms, while Edge measured 4.9, 4.9 and 4.8 ms. The B-versus-C access difference is therefore not established either way. A dedicated benchmark (isolated page, more rounds) should settle it before anyone quotes C's access overhead. The correctness results, and A's copy cost on large arrays, were stable.

## Decision

Adopt **C**, the hybrid model:

- **Wasm GC for every reference-holding object.** Classes, delegates, closures and arrays of references stay in Wasm GC. Object identity, cycles and JS interop keep working without a custom collector.
- **Linear-memory payloads for pinnable data.** Arrays with a blittable element type and `string` are GC wrappers over a linear-memory payload. `ldelem`/`stelem` load and store through the payload address after a bounds check against the wrapper's length. `fixed` and `ldelema` (when pinned) yield the payload address without copying.
- **One `Span<T>` representation.** `Span<T>`/`ReadOnlySpan<T>` over blittable data is `(address, length)`, the same as a `stackalloc` span. `Memory<T>` keeps the wrapper reference.
- **Shadow stack for addressed locals.** Address-taken locals and `stackalloc` use a linear shadow stack, popped on every exit path, including exceptional unwinding.
- **Reclamation.** A payload is freed when its wrapper is collected (`FinalizationRegistry` or a successor mechanism), through a free-list allocator. The compiler keeps the wrapper alive for the whole `fixed` scope, like a CLR pinned local, so a pointer never outlives its storage within the scope.

Rejected:
- **A** is incorrect. The CLR makes managed and pointer accesses to a pinned array alias, and a copy breaks that (stage 77 `FixedAliasing`). It is also linear in array size on every `fixed`.
- **B** is correct and as fast for these probes, but it gives up Wasm GC entirely. It would need precise root scanning of a shadow copy of every reference, its own collector, and manual handling of references held by JS. It brings no gain over C on any probe.

## Consequences and open items

- **Allocator.** The prototype uses a bump allocator. The compiler needs a size-class free-list allocator for payloads, with zero-initialization (the CLR guarantees zeroed arrays) and growth via `memory.grow`.
- **Lazy reclamation.** `FinalizationRegistry` callbacks are lazy and run only between tasks. Programs that allocate large blittable arrays in a tight synchronous loop can hold dead payloads until the next turn. A follow-up measures peak linear memory under such a loop and decides whether `Array.Resize`/`stackalloc`-heavy framework paths need an explicit release hook.
- **Registration cost.** Registration costs about 60–75 ns per allocation here, paid only for pinnable allocations. If profiles show it matters, small arrays can share registered slabs.
- **Interior pointers into class fields.** `fixed (int* p = &obj.Field)` (pinning a field of a class instance) is not covered by C, because class instances stay in Wasm GC. The compiler compiles the whole program, so it can find every type whose fields are ever pinned and give those types C-style linear storage too. That extension needs its own fixture before it lands. Unpinned managed byrefs (`ref obj.Field`) can stay `(object, field)` pairs.
- **Diagnostics until implemented.** Until the portable compiler implements this model, IL that pins or takes the address of managed memory remains an explicit unsupported diagnostic at the method and IL offset.
