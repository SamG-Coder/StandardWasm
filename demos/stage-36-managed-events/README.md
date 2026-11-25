# Stage 36 · Managed event sender and arguments

Run from the repository root.

    npm run demo -- stage-36-managed-events

The compiler takes the .NET Standard DLL and emits a Wasm module that returns 1 from `ArgumentIdentity(0)`. Within one module instance, `EventArgs.Empty` always returns the same reference and each `new EventArgs()` returns a fresh reference. An immutable global initialized with `struct.new_default` holds the singleton. The compiler validates the .NET Standard facade's AssemblyRef name and public key token before binding those members. [WebAssembly Core Specification](https://webassembly.github.io/spec/core/valid/instructions.html#valid-constant)

`SenderIdentity` and `SenderLocal` verify that a sealed class instance passes through an `object` parameter, return, local or field without being copied. Wasm GC uses `eqref` for the wider type and `ref.eq` for identity. The CLR oracle covers separate instances, distinct classes and default-null fields. It also checks `EventArgs` signatures and field storage, and the compiler rejects a modified AssemblyRef token at the load instruction.

## Event subscription fixtures

The DLL also contains the CLR oracle for event dispatch. The portable compiler still needs `EventHandler` signatures and invocation-list support, so these entries currently run on the CLR.

| CLR entry in `EventLibrary.EventCases` | Expected | Behavior |
| --- | --- | --- |
| `SubscriptionOrder()` | 12112 | Invoke in registration order and remove the last matching subscription. |
| `RemoveDuringRaise()` | 323 | Keep the current invocation list intact when a handler removes another handler. |
| `EmptyAndUnmatched()` | 2 | An empty event and removal of an unregistered handler leave other behavior intact. |
| `DistinctTargets()` | 1 | Removal matches the target and method. |

Every handler checks that the sender is the original source and the arguments are the same object as `System.EventArgs.Empty`.

The release build generates event accessors that call `System.Delegate.Combine` or `System.Delegate.Remove`, then use generic `Interlocked.CompareExchange` on the delegate field. The raise method uses a field snapshot followed by `EventHandler.Invoke(object, EventArgs)`.
