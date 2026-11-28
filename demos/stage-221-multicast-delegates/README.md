# Stage 221 · Multicast delegates

Run `npm run demo -- stage-221-multicast-delegates` on Windows with the .NET 10 SDK, the x86
runtime, Node and Microsoft Edge. Build the self-hosted compiler with `npm test` first.

Stage 204 compiled `Func`, `Action` and library-declared delegates, but each value held a single
target and method. `+=`, `Delegate.Combine`, field-like events and casts from `Delegate` stayed
diagnosed. WinForms relies on all of them: `Control.Events` keeps every handler as a
`System.Delegate` and casts it back with `(PaintEventHandler)Events[key]`.

Now a general delegate is an invocation list, the same physical list `EventHandler` uses. Each
entry records the delegate's nominal type, so:

- `Combine` and `Remove` work on any delegate type. Mixing two types raises `ArgumentException`,
  as on the CLR.
- invoking a multicast delegate calls every entry in order and returns the last result
- `castclass` and `isinst` from `Delegate` or `object` check the nominal type. `Notify` and
  `Alert` share a signature but stay different types.

The fixture is an ordinary .NET Standard 2.0 library. The x86 CLR runs it as the oracle, and Node
and Edge must return the CLR's value for every kernel:

- invocation order and the last result of a multicast `Func`
- removing a duplicate (the last one goes), the last occurrence of a pair, an absent delegate and
  the sole entry (which leaves `null`)
- combining with nulls
- an exception that stops the remaining handlers
- lambdas over instance targets
- C# field-like events of custom delegate types, which compile to `Combine` + `castclass` +
  `Interlocked.CompareExchange<T>`
- a `Delegate` slot like `Control.Events`, read back with a downcast, including when it is empty
- casts and `is` tests from `object`, a wrong nominal type (`InvalidCastException`) and an
  `EventHandler` cast of another delegate type
- `Combine` and `Remove` of mismatched types (`ArgumentException`)
- `Action<int>` and `Action<uint>` as distinct types
- `CompareExchange` identity on a delegate field
- `==` and `!=`: same and different methods and targets, nulls, multicast order, different
  nominal types and EventHandler

The `Reject*` kernels must be diagnosed. `Func<int>` keeps decision 0088's single closure, so it
can't be combined or stored as `object`. `Delegate.Equals(object)` isn't lowered yet. The
design is decision 0092.
