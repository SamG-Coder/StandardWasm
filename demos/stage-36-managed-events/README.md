# Stage 36 · Bound event handlers

Run from the repository root.

    npm run demo -- stage-36-managed-events

The compiler takes the .NET Standard DLL and emits a Wasm module that returns 12 from `InvokeBoundHandler(0)`. The fixture stores an ordinary `System.EventHandler` in a field and invokes it with the original sender and `EventArgs.Empty`. The compiler creates a Wasm GC struct for the delegate's target and typed function reference. A thunk receives the target, sender and arguments, casts the target to its concrete class, and forwards the call. The fixture exercises distinct methods, distinct targets, and a null delegate. The CLR's `NullReferenceException` corresponds to a Wasm trap.

Within one module instance, `EventArgs.Empty` always returns the same reference and each `new EventArgs()` returns a fresh reference. An immutable global initialized with `struct.new_default` holds the singleton. The compiler validates the .NET Standard facade's AssemblyRef name and public key token before binding those members. [WebAssembly Core Specification](https://webassembly.github.io/spec/core/valid/instructions.html#valid-constant)

`SenderIdentity` and `SenderLocal` verify that a sealed class instance passes through an `object` parameter, return, local or field without being copied. Wasm GC uses `eqref` for the wider type and `ref.eq` for identity. The CLR oracle covers separate instances, distinct classes and default-null fields. It also checks `EventArgs` signatures and field storage, and the compiler rejects a modified AssemblyRef token at the load instruction.

## Event subscription fixtures

The current Wasm implementation covers a single bound, nonvirtual instance handler. Static handlers, multicast delegate operations and compiler-generated event accessors still need support. The remaining subscription fixtures currently run on the CLR.

| CLR entry in `EventLibrary.EventCases` | Expected | Behavior |
| --- | --- | --- |
| `SubscriptionOrder()` | 12112 | Invoke in registration order and remove the last matching subscription. |
| `RemoveDuringRaise()` | 323 | Keep the current invocation list intact when a handler removes another handler. |
| `EmptyAndUnmatched()` | 2 | An empty event and removal of an unregistered handler leave other behavior intact. |
| `DistinctTargets()` | 1 | Removal matches the target and method. |

Every handler checks that the sender is the original source and the arguments are the same object as `System.EventArgs.Empty`.

The release build generates event accessors that call `System.Delegate.Combine` or `System.Delegate.Remove`, then use generic `Interlocked.CompareExchange` on the delegate field. The raise method uses a field snapshot followed by `EventHandler.Invoke(object, EventArgs)`.
