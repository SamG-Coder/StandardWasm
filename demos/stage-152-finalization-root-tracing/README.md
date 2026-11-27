# Stage 152 · Finalization root tracing

Run `npm run demo -- stage-152-finalization-root-tracing` after building the
self-hosted compiler with `npm test`. Install matching .NET 10 runtimes for
the SDK host and Windows x86. The runner compiles the same .NET Standard
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

The diagnostic step invokes the managed destructor selected by the original
virtual slot. It roots the receiver while its callback runs and uses the
original x86 CoreLib for the final call to `Object.Finalize`. Both engines
match the CLR's destructor and base-call effects. The overriding destructor
resurrects the same receiver through a managed static. Clearing that static
and discovering again never schedules another callback unless registered again.

Ordinary `GC.SuppressFinalize` calls in this fixture resolve through the
trusted CoreLib definition to the probe's suppression operation. Repeated
calls are idempotent, and a suppression marker can suppress one registered
or already pending entry. The tests include the null exception tag,
non-finalizable objects, and multiple registrations for one receiver. The
tag carries the exception kind. The probe does not expose the CLR exception
object or `ParamName`.
[CoreLib suppression implementation](https://github.com/dotnet/runtime/blob/v10.0.10/src/coreclr/System.Private.CoreLib/src/System/GC.CoreCLR.cs#L317-L330)

Binding re-registration and scheduling collection remain open, along with host roots,
event-delegate traversal, indirect calls, and a cleanup policy for raw Wasm
traps. General executable filters are outside the compiler's supported
pure type-disjunction pattern. Addressed slots are kept live
conservatively. For a mutable frame cell, the root is its backing storage.
Managed location and value-cell wrappers expose their owner or snapshot to
the mark pass. Node and Edge verify that an addressed struct's reference and
an updated `ref object` cell both survive the protected call and its `finally`,
then become unreachable after the frame returns. JavaScript invokes the pass
at probe observation points. The Wasm module performs the traversal.

A protected-frame fixture compares a fixed divide-by-zero exception and a
managed exception with the CLR. Both engines execute the `finally` and
filtered catch, and the managed payload is marked during both calls. Rethrow
preserves the original object and clears its published root as the frame
unwinds. The fixed-tag case keeps its tag and has no managed payload.

The pinning test observes a string whose only managed root is its active pin.
The mark pass visits the owner in each pin registry pair. The paired native
address is used for memory access. Both engines read the CLR's UTF-16 code
unit and release the pin on normal return and managed exception unwind.

The host oracle also binds the original CoreLib resource prelude and checks
its two implicit roots. The assembly descriptor and name are live during
lookup. The managed tail continues to use the assembly when it constructs
the stream. The name has no further uses. This is a compiler check against
the installed CoreLib IL. The standalone resource-opening fixture still
reaches an unsupported dependency in CoreLib's resource-string path.
