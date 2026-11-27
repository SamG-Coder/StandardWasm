# Stage 155 · Bound catch variables

Run `npm run demo -- stage-155-bound-catch-variables` on Windows with the .NET 10 SDK, the x86
runtime, Node and Microsoft Edge. Build the self-hosted compiler with `npm test` first.

Until this stage a catch handler had to discard its exception. `catch (Exception e)` that reads
`e` was rejected. Now the handler receives the caught object:

- **A thrown exception object** arrives as itself.
- **A runtime-raised divide-by-zero, null dereference, index-out-of-range or overflow** arrives as
  the CoreLib exception the CLR would have raised. A rethrow keeps that same object.

The fixture is an ordinary .NET 10 library. The x86 CLR runs it as the oracle. The runner bundles
it with the x86 `System.Runtime` and `System.Private.CoreLib`, and Node and Edge must return the
CLR's value for every kernel:

- a caught object, its `HResult`, a library exception class, and a base-class catch of a derived
  exception
- `InnerException` read in a handler
- the four runtime-raised exceptions caught as `Exception`, with their CLR `HResult`s
- `throw;` identity for a runtime-raised and a thrown exception
- nested bound handlers
- issue #170's `TypeInitializationException`: the same object on a second trigger, and its inner
  exception

One case differs on purpose. A failed cast reaching a bound handler stops with an explicit trap,
because the CLR's message names both types and the cast site doesn't supply them. The CLR returns
7 for that kernel.

`e.Message` waits on passing a string as `object`, which is #232's Stage 133 work. The design is
decision 0082.
