# Stage 181 · CoreLib event accessors

Run `npm run demo -- stage-181-corelib-event-accessors` on Windows with the .NET 10 SDK, the x86
runtime, Node and Microsoft Edge. Build the self-hosted compiler with `npm test` first.

The unchanged WinForms application subscribes to `AppDomain.CurrentDomain.ProcessExit` while it
starts. CoreLib's add and remove accessors for that event are its own field-like event loops: they
call `Delegate.Combine` or `Delegate.Remove`, cast the result to `EventHandler`, and swap the field
with `Interlocked.CompareExchange`. Inside CoreLib those calls name CoreLib's own methods directly,
which the compiler didn't recognize until this stage.

The fixture is an ordinary .NET Standard 2.0 library, and every kernel runs the original CoreLib
accessors. The x86 CLR runs it as the oracle, and Node and Edge must return the CLR's value:

- a static handler subscribed and removed
- an instance handler subscribed and removed
- three add/remove pairs in a row
- removing a handler that was never added
- adding and removing `null`
- the CoreLib accessor beside a library class's own event, which is still raised correctly

The design is decision 0090.
