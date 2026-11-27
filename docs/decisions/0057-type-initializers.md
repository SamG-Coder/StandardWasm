# 0057 · Run reachable type initializers

Status is proposed in #170.

The original WinForms `Application.EnableVisualStyles` stores a static field
of a type with an initializer, and CoreLib's `SR` reads one. Until now any
static field of a type with a `.cctor` was rejected. Stage 129 runs reachable
type initializers with CLI trigger semantics (ECMA-335 II.10.5.3). It also
allocates plain `System.Object` instances, which the same `Application`
initializer needs.

## Guards

Each reached `.cctor` compiles as its own Wasm function. It is reached only
through its guard, an i32 global with four states:

| State | Value | A trigger in this state |
| --- | --- | --- |
| not started | 0 | marks it running and runs the body |
| running | `(epoch << 2) \| 1` | returns at once if `epoch` is current (recursion); otherwise traps |
| completed | 2 | returns at once |
| failed | 3 | traps |

The managed program has one thread (decision 0026). A trigger reached while
the same initializer is running is therefore recursion on that thread, and the
CLR also returns immediately, exposing partly initialized statics. Each IL
`ret` of the body marks the guard completed. An initializer never tail-calls:
it must observe its callees' failures.

## Triggers

- **Static field access.** Any `ldsfld` or `stsfld` of a field whose bound
  declaring type has an initializer calls the guard first. That covers both
  beforefieldinit and precise types. A store calls it after evaluating the
  right-hand side and before the write. The CLR logs `right-hand,initializer`.
  The guard belongs to the declaring owner, so a field reached through a
  derived class initializes only its declaring base.
- **Precise types** (no beforefieldinit). Every static method and instance
  constructor calls the guard on entry, after the caller has evaluated the
  arguments. The CLR logs `argument,initializer`. Entry placement also covers
  `this(...)` and `base(...)` constructor chaining. A delegate to a static
  method of a precise type is still rejected at its `ldftn` by the delegate
  binder (Stage 119). If that binder accepts it later, the entry guard already
  covers the call.
- **beforefieldinit.** The type waits for its first static field access. A
  static call alone does not trigger it, which keeps Stage 108's rule. The CLR
  may run such an initializer earlier, so fixtures check only "once, before
  the read".

`initonly` static fields may be written only by the declaring type's own
initializer in the same module (ECMA-335 II.16.1.2). Every other write stays
rejected.

## Failure

The CLR wraps an escaping exception in `TypeInitializationException`. It
makes one attempt and raises the same exception object, with the same inner
exception, on every later trigger. The compiler cannot construct that
exception yet. The constructor needs CoreLib's resource formatting (#170,
#174), and fixed-tag exceptions carry no inner object. So a failure must stop
execution, never reach managed code unwrapped, and never expose a partly
initialized type:

- The body runs inside `try_table (catch_all)`. A managed exception that
  escapes, on a fixed tag or the object tag, marks the guard failed. It then
  advances the module's failure epoch and executes `unreachable`. A later
  trigger on the same instance traps again, without a second attempt.
- The trap unwinds every frame. Any initializer still marked running with an
  older epoch was interrupted by that failure and reads as failed.
- A raw engine trap, such as stack exhaustion, bypasses `catch_all`. A second
  global counts initializers on the Wasm stack. When a module has
  initializers, every host export enters through a wrapper. If that count is
  nonzero, the wrapper advances the epoch and clears the count. Internal calls
  use the original bodies, so recursion within one invocation keeps its epoch.
  This assumes the host never re-enters an export while an initializer runs.
- The trap is an explicit runtime diagnostic. A CLR program that catches the
  `TypeInitializationException` and continues does something this compiler
  cannot yet reproduce. #170 stays open for that gap.

## Typed failure (#170)

When the bundle supplies CoreLib, a **managed exception object** escaping an initializer body
becomes the CLR's `TypeInitializationException`:

- **Catching.** The guard's `try_table` catches the object tag with its payload, separately
  from `catch_all`.
- **Construction.** It builds CoreLib's own `TypeInitializationException` through the upstream
  internal constructor `(string fullTypeName, string message, Exception innerException)`:
  - `fullTypeName` is `Type.FullName` (`Namespace.Outer+Inner`), written at compile time.
  - `message` is `The type initializer for '<fullTypeName>' threw an exception.`
  - The constructor's untaken `SR` fallback compiles because #304 folds CoreLib resource
    strings (decision 0081).
- **Caching.** The object goes in a per-initializer global (owner 0, a reserved negative row).
  The guard is marked failed and the object is thrown on the object tag. Every later trigger
  rethrows the **same** object, as the CLR does.
- **No epoch advance.** A managed exception unwinds frame by frame, so each enclosing
  initializer's guard catches and wraps it in turn, and the epoch does not advance. An
  initializer that reads a failing one fails with a `TypeInitializationException` whose inner
  exception is the first.

**Fixed-tag exceptions** carry no object. The guard catches each one into its own block,
which builds the matching CoreLib exception with its parameterless constructor and passes it as
the inner exception. The default message comes from `SR` (decision 0081). This is exact only
where the CLR raises the exception with that same default message:

| Tag | CoreLib exception | Wrapped |
| --- | --- | --- |
| Overflow | `OverflowException` | yes |
| DivideByZero | `DivideByZeroException` | yes |
| IndexOutOfRange | `IndexOutOfRangeException` | yes |
| NullReference | `NullReferenceException` | yes |
| InvalidCast | `InvalidCastException` | no: the CLR's message names both types |
| Argument | `ArgumentException` | no: the CLR's message names the parameter |
| ArgumentNull | `ArgumentNullException` | no: the CLR's message names the parameter |

A tag marked "no" keeps the trap rather than build an object whose `Message` would differ.
It can be wrapped once the raising site supplies the exact message.

These cases still trap as described above:
- a tag whose CoreLib type cannot be bound
- a bundle without CoreLib's constructors
- a type name that is not ASCII
- raw engine traps such as stack exhaustion



`newobj System.Object::.ctor` allocates the reserved zero-field struct in the
native-vector recursive group (`RootObjectHeap`). Each allocation has its own
reference identity, and no class's nominal test accepts it. Object members
called on it (`GetHashCode`, `ToString`, `GetType`, `Equals`) and `lock`
(`Monitor.Enter`) stay located diagnostics.

## Still diagnosed

- generic types with an initializer, which need per-instantiation guards
- instance methods of precise value types, which also trigger
- `RuntimeHelpers.RunClassConstructor`
- module initializers beyond the empty and trivial-call forms
- static field addresses (`ldsflda`)
