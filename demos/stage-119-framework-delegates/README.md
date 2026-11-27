# Stage 119 · Framework delegates

Run `npm run demo -- stage-119-framework-delegates`. The linked demo returns
18171061. The full CLR, Node and Edge suite runs with `npm test`.

A compiled .NET app reaches `System.EventHandler`, `System.EventArgs`,
`System.Delegate` and `System.Threading.Interlocked` through System.Runtime
and System.Threading, not through the .NET Standard facade. Both net10.0
assemblies in this fixture are built that way, as the original WinForms
executable is. The compiler binds these names by platform identity to its own
EventHandler invocation-list representation, in every bundled assembly. A
linked signature that takes an `EventHandler` therefore matches by identity,
without bundling the System.Runtime facade. The negative fixtures verify
that EventHandler through System.Runtime.Extensions and Interlocked through
System.Runtime fail to bind, as they do on the installed CLR.

The fixture compares the following against the CLR:

- subscribing through a linked field-like event's `add_` accessor and
  raising it from the publisher, with the publisher as `sender`
- a lambda closure target, like the WinForms constructor's handler
- `Delegate.Combine` order, and `Delegate.Remove` of the last matching
  occurrence and of a missing handler
- unsubscribing through the linked `remove_` accessor
- delegate equality as `Remove` observes it, over the same target and method
  versus a different target
- a static target that updates shared state when the delegate invokes it

Stage 164 covers `Func<int>` and open static targets. General delegate types,
`ldvirtftn`, `System.Delegate`-typed storage in framework code, and subclasses
of the platform `EventArgs` remain unsupported.
