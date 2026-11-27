# Stage 152 · Finalization root tracing

Run `npm run demo -- stage-152-finalization-root-tracing` after building the
self-hosted compiler with `npm test`. The runner compiles the same .NET Standard
2.0 DLL in Node and Microsoft Edge, executes the resulting standalone Wasm, and
compares seven cases with .NET 10. Results go to `artifacts/finalization-root-tracing/`.

The experiment uses real Wasm GC references to model registered objects and an
explicit worklist to trace ordinary roots and pending receivers. The queue
keeps a receiver and its fields available until its callback runs. The cases
exercise repeated suppression, transitive roots, repeated re-registration,
resurrection, suppression after discovery, and a pending receiver whose child
becomes eligible only after the pending receiver leaves the queue. The expected
checksums are `23, 0, 160, 0, 58, 63, 140`.

The CLR oracle independently runs three of the cases using actual CLR
finalizers. After two calls to `GC.SuppressFinalize` followed by two calls to
`GC.ReRegisterForFinalize`, the original registration is re-enabled and the
second re-registration adds an entry. Both callbacks can run before the waiter
returns. This behavior was measured on .NET 10.0.10. The other four cases
exercise the experiment's explicit discovery and drain steps.
[CoreCLR registration implementation](https://github.com/dotnet/runtime/blob/v10.0.10/src/coreclr/gc/gc.cpp#L48323-L48336)

The fixture supplies its roots and field traversal explicitly. Runtime
integration needs compiler-emitted roots for live frames, statics and host
references, and type-specific traversal covering object fields, reference
arrays and managed interior references. Scheduling callbacks and binding the
original CoreLib APIs remain part of the WinForms startup milestone.

The host oracle also checks the compiler's frame-slot analysis on the same
fixture DLL. It covers straight-line uses, branches, switches, back edges and
overwriting an argument. An address-exposed slot or an exception region needs
additional analysis. The live bits describe frame slots before a given IL
instruction. Values already loaded onto the evaluation stack are handled
separately as part of runtime integration.

An internal compiler mode emits a root stack around direct managed calls
and constructors in the probe fixture. It records live frame slots, references
in the evaluation stack that survive the call, and a newly allocated receiver
while its constructor is running. Node and Edge observe three occupied slots
with two distinct objects during a nested call, and two slots referring to
the same receiver during an active constructor. They observe one slot during
the throwing call and an empty stack after each normal return or managed
exception unwind. The normal compilation entry point keeps the diagnostic
table private by omitting it entirely.

The same module emits typed reads of reference fields and reference arrays.
The Wasm mark pass starts from published frames and ordinary managed statics.
It follows inherited fields, array elements and a cycle, using reference
identity to visit aliases once. Both engines find the CLR's five reachable
objects. The verifier moves the graph from a frame to a static and checks
that clearing the static removes all five objects from the next mark set.
Byte arrays are leaves, and scalar fields do not take an edge index.

The mark list retains its references until the next mark pass or explicit
clear. Its linear identity lookup makes this a prototype for small graphs.
The Wasm discovery pass uses the marks to move unreachable registrations to
a pending queue while keeping rooted registrations. Pending receivers seed
later marks so their children remain available. The probe verifies that a
child is eligible only after its pending parent leaves the queue, that a
pending receiver retains its original identity and fields, and that duplicate
registrations produce separate entries.

The probe reads the trusted CoreLib `Object.Finalize` declaration and uses
the allocated type's virtual slot, including the explicit override metadata
emitted by C#. Allocation registers the receiver before entering its
constructor. The CLR, Node and Edge agree on four eligible receivers covering
direct, inherited and overriding destructors and a constructor that throws.
The unrelated virtual method named `Finalize` creates a different slot and
does not register its receiver.

The diagnostic exports also support manual queue tests. The allocation test
uses automatic registration and inspects the pending receivers. Running
their managed callbacks and binding the original CoreLib suppression APIs
remain open, along with host roots,
traversal of runtime wrappers such as view owners and event delegates,
managed addresses, protected regions, indirect calls and a cleanup policy for
raw Wasm traps. JavaScript invokes the pass at a probe observation point and
checks the result. The Wasm module performs the worklist and edge traversal.
