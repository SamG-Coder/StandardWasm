# Stage 36 · Managed event subscriptions

Build the .NET Standard fixture from the repository root.

    dotnet build demos/stage-36-managed-events/EventLibrary.csproj -c Release

| CLR entry in `EventLibrary.EventCases` | Expected | Behavior |
| --- | --- | --- |
| `SubscriptionOrder()` | 12112 | Invoke in registration order and remove the last matching subscription. |
| `RemoveDuringRaise()` | 323 | Keep the current invocation list intact when a handler removes another handler. |
| `EmptyAndUnmatched()` | 2 | An empty event and removal of an unregistered handler leave other behavior intact. |
| `DistinctTargets()` | 1 | Removal matches the target and method. |

Every handler checks that the sender is the original source and the arguments are the same object as `System.EventArgs.Empty`.

The release build generates event accessors that call `System.Delegate.Combine` or `System.Delegate.Remove`, then use generic `Interlocked.CompareExchange` on the delegate field. The raise method uses a field snapshot followed by `EventHandler.Invoke(object, EventArgs)`.

The current compiler rejects the fixture before lowering an IL instruction. Once event support is implemented, `npm run demo -- stage-36-managed-events` will compile the fixture to Wasm and return 12112.
