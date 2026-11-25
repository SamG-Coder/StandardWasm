# Stage 36 · Managed event sender identity

Run from the repository root.

    npm run demo -- stage-36-managed-events

The compiler takes the .NET Standard DLL and emits a Wasm module that returns 1 from `SenderIdentity(0)`. The same sealed class instance passes through an `object` parameter, return, local or field without being copied. Wasm GC uses `eqref` for the wider type and `ref.eq` for identity. The CLR oracle also compares separate instances, instances of different classes and the default-null object field. The supported conversions here are between same-module sealed classes and `object`.

## Event subscription fixtures

The DLL also contains the CLR oracle for event dispatch. The portable compiler still rejects the event signatures, so these entries currently run on the CLR.

| CLR entry in `EventLibrary.EventCases` | Expected | Behavior |
| --- | --- | --- |
| `SubscriptionOrder()` | 12112 | Invoke in registration order and remove the last matching subscription. |
| `RemoveDuringRaise()` | 323 | Keep the current invocation list intact when a handler removes another handler. |
| `EmptyAndUnmatched()` | 2 | An empty event and removal of an unregistered handler leave other behavior intact. |
| `DistinctTargets()` | 1 | Removal matches the target and method. |

Every handler checks that the sender is the original source and the arguments are the same object as `System.EventArgs.Empty`.

The release build generates event accessors that call `System.Delegate.Combine` or `System.Delegate.Remove`, then use generic `Interlocked.CompareExchange` on the delegate field. The raise method uses a field snapshot followed by `EventHandler.Invoke(object, EventArgs)`.
