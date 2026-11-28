# Stage 204 · General delegate signatures

Run `npm run demo -- stage-204-general-delegates` on Windows with the .NET 10 SDK, the x86
runtime, Node and Microsoft Edge. Build the self-hosted compiler with `npm test` first.

Until this stage, a delegate had to be `Func<int>`, `EventHandler` or the `SpanAction<char, IntPtr>`
callback. `Func<int, int>`, `Action<string>`, `Func<long, int, long>` and delegates the library
declares itself, such as `delegate long Combine(long a, int b)`, didn't compile.

Now each closed `System.Func` (one to five type arguments), `System.Action` (one to four) and each
non-generic delegate type declared in a compiled library gets its own delegate shape when every
parameter and the result have Wasm storage. Those are integers, floating point, native integers,
strings, `object`, classes and other delegates. All shapes share the closure struct used since Stage
164 (target, function reference). Each shape adds a thunk function type and an invoke helper that
casts the function reference to that type before `call_ref`.

The fixture is an ordinary .NET Standard 2.0 library. The x86 CLR runs it as the oracle, and Node
and Edge must return the CLR's value for every kernel:

- static method groups for `Func<int, int>`, `Func<string, int>`, `Func<object, object>`,
  `Func<long, int, long>` and `Func<double, double>`
- `Action<int>` called twice against a static counter
- instance targets, captured lambdas and cached lambdas passed to a method
- delegates stored in static and instance fields, then passed on
- library-declared delegates with static, void, instance (captured) and class-result signatures
- invoking a null delegate (`NullReferenceException`)
- `Slot<Action<int>>` and `Slot<Action<uint>>` stay distinct types with their own static fields,
  and `Func<uint>` is a general delegate, not decision 0088's `Func<int>`
- 80 distinct delegate types in one method, so the module's type count and thunk type indices
  need multi-byte LEB encodings

Stage 204 diagnosed casts from `object`, `is` tests and `Delegate.Combine` (`+=`) on these
delegates, because the closure had no type identity. Stage 221 (decision 0092) adds that identity,
so those four kernels now return the CLR's values. `RejectSelfReference`, a delegate whose
`Invoke` takes its own type, must still be diagnosed. The design is decision 0091.
