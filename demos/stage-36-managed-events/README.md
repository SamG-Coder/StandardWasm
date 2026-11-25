# Stage 36 · Multicast event handlers

Run from the repository root.

    npm run demo -- stage-36-managed-events

The compiler takes the .NET Standard DLL and emits a Wasm module that returns 123 from `RemoveCallbacks(0)`. That case removes the last matching two-callback subsequence from a five-callback list. The other cases cover middle removal, no match, null operands, removing the entire list, and removing a callback while dispatch is in progress. When one callback survives, the result preserves that original delegate's identity. [Delegate.Remove](https://learn.microsoft.com/en-us/dotnet/api/system.delegate.remove?view=net-10.0)

`CombineCallbacks` checks registration order, null operands, and that a callback added during dispatch first runs on the next invocation. [Delegate.Combine](https://learn.microsoft.com/en-us/dotnet/api/system.delegate.combine?view=net-10.0)

The compiler represents each bound target and typed function reference with a Wasm GC struct. A private GC array stores those entries in invocation order. Combining two nonnull delegates copies both lists into a new array, and each invocation keeps its original array for the duration of dispatch. `InvokeBoundHandler` covers distinct methods and target objects. Invoking a null delegate traps in Wasm and throws `NullReferenceException` on the CLR.

Within one module instance, `EventArgs.Empty` always returns the same reference and each `new EventArgs()` returns a fresh reference. An immutable global initialized with `struct.new_default` holds the singleton. The compiler validates the .NET Standard facade's AssemblyRef name and public key token before binding those members. [WebAssembly Core Specification](https://webassembly.github.io/spec/core/valid/instructions.html#valid-constant)

`SenderIdentity` and `SenderLocal` verify that a sealed class instance passes through an `object` parameter, return, local or field without being copied. Wasm GC uses `eqref` for the wider type and `ref.eq` for identity. The CLR oracle covers separate instances, distinct classes and default-null fields. It also checks `EventArgs` signatures and field storage, and the compiler rejects a modified AssemblyRef token at the load instruction.

## Event subscription fixtures

The current Wasm implementation covers combining, removing and invoking bound, nonvirtual instance handlers. Static handlers and compiler-generated field-like event accessors still need support. The remaining subscription fixtures currently run on the CLR.

| CLR entry in `EventLibrary.EventCases` | Expected | Behavior |
| --- | --- | --- |
| `SubscriptionOrder()` | 12112 | Invoke in registration order and remove the last matching subscription. |
| `RemoveDuringRaise()` | 323 | Keep the current invocation list intact when a handler removes another handler. |
| `EmptyAndUnmatched()` | 2 | An empty event and removal of an unregistered handler leave other behavior intact. |
| `DistinctTargets()` | 1 | Removal matches the target and method. |

Every handler checks that the sender is the original source and the arguments are the same object as `System.EventArgs.Empty`.

The release build generates event accessors that call `System.Delegate.Combine` or `System.Delegate.Remove`, then use generic `Interlocked.CompareExchange` on the delegate field. The raise method uses a field snapshot followed by `EventHandler.Invoke(object, EventArgs)`.
