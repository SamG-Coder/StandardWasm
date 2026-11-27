# Stage 153 · Missing dependencies

Run `npm run demo -- stage-153-missing-dependencies` on Windows with the .NET 10 SDK, the x86
runtime, Node and Microsoft Edge. Build the self-hosted compiler with `npm test` first.

The fixture is an ordinary .NET 10 library that references `MissingDependency.dll`. The oracle
runs it on the CLR with that DLL absent:

- Code that does not need the dependency runs normally.
- Each call into a method whose body needs the dependency throws `System.IO.FileNotFoundException`:
  `Could not load file or assembly 'MissingDependency, Version=1.0.0.0, Culture=neutral,
  PublicKeyToken=null'. The system cannot find the file specified.`
- The exception comes from the CLR's JIT failing that method, so the method's own handlers
  never run and its caller receives the exception. It is thrown again on every call.

The runner bundles the fixture with the 32-bit `System.Runtime` and `System.Private.CoreLib` and
leaves the dependency out. It compiles each kernel with the self-hosted compiler's opt-in
`CompileBundleDeferred`, which compiles each such method to a stub. The stub throws the
bundled CoreLib's own `FileNotFoundException`, with the CLR's message and file name. Node and
Edge must return the CLR's value for every case. An exception that escapes reaches the host as
a managed exception on the object tag, not as a trap.

The kernels cover:

- untouched code
- a reference-typed field of a missing class (the CLR does not load it)
- a reference-typed local of a missing class (the CLR does not load it)
- `new` of a missing type, a static call into it, a missing base type and a value-type field
- a missing type in a callee's signature
- a branch that is never taken (the whole method fails, as on the CLR)
- `catch (Exception)`
- a failing method whose own handler stays unentered
- three repeated calls
- an exception that escapes the entry

The strict `CompileBundleDetailed` keeps its located -4 for the same kernels. The design and
its limitations are in decision 0080.
