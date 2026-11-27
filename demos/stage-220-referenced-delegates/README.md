# Stage 220 · Delegates from referenced assemblies

Run `npm run demo -- stage-220-referenced-delegates` on Windows with the .NET 10 SDK, the x86
runtime, Node and Microsoft Edge. Build the self-hosted compiler with `npm test` first.

Stage 204 compiled delegate types a library declares itself. An application usually uses delegate
types from its references instead. WinForms' `ThreadWindows` constructor, for example, builds a
`PInvokeCore.EnumThreadWindowsCallback(HWND) -> BOOL` declared in `System.Private.Windows.Core`.
Those calls name the delegate through a TypeRef, and they stayed diagnosed.

Now a MemberRef `.ctor` or `Invoke` on a TypeRef that resolves to a delegate TypeDef in another
bundle assembly uses that TypeDef's delegate key. Keys are bundle-wide, so methods in either
assembly can take, return and invoke the same delegate. Value-type parameters and results, such as
Win32 wrapper structs, pass as in any other call.

The fixture is an ordinary .NET Standard 2.0 library with a .NET Standard 2.0 dependency that
declares the delegates. The x86 CLR runs both as the oracle, and Node and Edge must return the
CLR's value for every kernel:

- static, void and class-result delegates from the dependency, constructed and invoked in the app
- a null dependency delegate (`NullReferenceException`)
- app lambdas invoked by the dependency, and a dependency-built delegate invoked by the app
- a nested `Native.EnumCallback(Handle, IntPtr) -> Flag` over wrapper structs, called back by a
  managed enumeration that stops when the callback returns 0

`CastFromObject` was diagnosed on Stage 220 itself. With Stage 221's nominal invocation lists
(decision 0092) it returns the CLR's value. Native callbacks, which pass a managed delegate
to an unmanaged function pointer, are separate work. The design is decision 0091.
