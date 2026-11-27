# Stage 164 · Delegate representation

Run `npm run demo -- stage-164-delegate-representation` on Windows with the .NET 10 SDK, the x86
runtime, Node and Microsoft Edge. Build the self-hosted compiler with `npm test` first.

C# compiles a lambda that captures nothing, such as `Run(() => 5)`, into a cached delegate in a
static field. Until this stage, that field and the delegate stored into it had different
representations, so the method didn't compile. Delegates over static methods (`Func<int> f = Five;`,
or a static method subscribed to an event) didn't compile either.

The fixture is an ordinary .NET Standard 2.0 library. The x86 CLR runs it as the oracle, and Node
and Edge must return the CLR's value for every kernel:

- cached lambdas, called twice (the second call reuses the cached delegate) and inside a `try`
- static method groups in a local, a static field and an instance field, invoked directly and
  passed on
- a static `EventHandler` subscribed, raised twice, removed and raised again
- a captured lambda beside a static method group
- invoking a null delegate (`NullReferenceException`)
- binding an instance method to a null target (`ArgumentException`, as on the CLR)

`Action`, `Func<int, int>` and other delegate signatures come in the next #256 milestone. The design
is decision 0088.
