# 0106 · Faster dispatch, root publishing and native struct copies

Status: proposed in #326 (Claude).

The benchmark profiles showed three costs that do not change any observable result: GC root
publishing around every call, linear state dispatch on every branch, and a managed copy for every
struct copied through pointers. Each is removed only where the result is provably identical.

## Root publishing only where a collection is reachable

With finalizer tracking (decision 0104), a collection starts only inside `GC.Collect` or
`GC.WaitForPendingFinalizers`, and it reads exactly the roots that the frames on the stack
published at their pending call sites. A frame's references therefore need publishing only around
calls that can reach one of those members.

- **May collect:** a method may collect when it calls those members, makes any call whose target is
  not a direct function (virtual, delegate, native, COM, reflection resource lookup), triggers a type
  initializer, or calls a method that may collect. Synthetic functions (thunks, dispatchers) may
  collect.
- **Effect:** only those methods keep a root frame, and only their calls to methods that may collect
  publish.
- **Keying:** the set is keyed by method identity (assembly, MethodDef row), because a pass that
  publishes fewer roots can number its functions differently. Every specialization of a method
  shares its key. The first tracking pass assumes everything collects; its call edges give the set,
  and the next pass compiles with it. The edges do not depend on the set, so that pass reproduces
  it. A set that keeps changing falls back to the conservative assumption, with one more pass.
- **Cell layout:** the managed value cell layout follows the module's tracking mode, not the frame's.

## Dispatch

Each method still compiles to a `pc` dispatch loop, and `pc` still holds the current IL offset at
every instruction, so exception dispatch and the helpers that use `pc` as scratch are unchanged.

- **Re-entry:** the loop re-enters with one `br_table` over the basic-block leaders, instead of a
  chain of `pc == state` tests.
- **Leaders:** the first state, every branch, leave and switch target, a conditional's fall-through,
  every exception clause boundary, resume point and handler, and any state after one that does not
  fall through. Methods with exception filters treat every state as a leader.
- **Blocks:** each leader's code follows the end of its block; the states after it fall through in
  `pc` order. Every state keeps its own label, as the former `if` gave it.

## Native struct copies

`ldobj T` immediately followed by `stobj T` between two native addresses (the store is not a
dispatch target) is one `memory.copy` of `sizeof(T)` bytes. `T` is blittable, so the bytes are the
CLR's. The source is null-checked first, as `ldobj` faults before `stobj`. `memory.copy` handles
overlap as the read-then-write does.

## Tail calls

A `call; ret` pair becomes `return_call` only when the frame has nothing to do at `ret`. Frames with
array pins, the scoped value pin or the pin registry now keep the frame too. Root tracking had hidden
this: a tracked frame never tail-called.

## Profiling names

An opt-in Wasm `name` section (`FunctionNames.EmitNames`, off by default) names each function
`Type::Method` for V8 profiles. Default output is unchanged.

## Measured (Node 24 against .NET 10 JIT, published sizes)

All workloads keep the CLR's `ops`, `checksum` and `verify`.

| Benchmark | Before | After |
|---|---|---|
| dlist | 3.8–69× | 1.1–6.7× |
| hashmap | 5.3–18× | 2.3–13× |
| rbtree | 3.0–14× | 1.7–5.9× |
| heap | 4.4–24× | 1.6–8.8× |
| btree | 5.7–31× | 2.5–7.3× |
| radix | 9.4–55× | 2.1–42× |
| skiplist | 2.5–22× | 1.3–5.3× |

unionfind's `find_deep` and `set_size` were slower in the one measured run (10× and 15×, from 3.6×
and 6.7×) while another process shared the machine. They are not yet re-measured.

Copy-based pinning (decision 0105) still copies the whole array per pin. That dominates hashmap
`scan`, radix `scan` and rtree's searches, whose driver pins one large output buffer per query, so
they are O(n²). Copying only the touched pages of a pin is the remaining work.
