# Stage 364 · Virtual delegate targets

Run `npm run demo -- stage-364-virtual-delegate-targets` on Windows with the .NET 10 SDK, the x86
runtime, Node and Microsoft Edge. Build the self-hosted compiler with `npm test` first.

A method group over a virtual method, such as `new EventHandler(this.OnClick)` for a
`protected virtual OnClick`, compiles to `dup; ldvirtftn M`. Until this stage `ldvirtftn` wasn't
decoded, so those delegates didn't compile. Now the delegate's thunk calls M's virtual dispatcher,
so invoking the delegate runs the target's own override, as on the CLR.

The fixture is an ordinary .NET Standard 2.0 library. The x86 CLR runs it as the oracle, and Node
and Edge must return the CLR's value for every kernel:

- an override through a base-typed reference, a sealed override and a non-overridden virtual
- the base class's own instance
- abstract and interface methods
- one `ldvirtftn` target bound through three receiver classes in one module, which exercises the
  shared dispatcher's arms
- an `EventHandler` over a virtual `OnTick` overridden in a subclass, with `+=`, `-=` and `==`
- a null receiver, which raises `NullReferenceException` at `ldvirtftn`

`RejectGenericVirtual` must be diagnosed. A generic virtual method's `ldvirtftn` names a MethodSpec,
and so does a method in another assembly. The design is decision 0093.
