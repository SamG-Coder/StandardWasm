# Stage 126 · Managed exception objects

Run `npm run demo -- stage-126-managed-exception-objects` on Windows with the .NET 10 SDK, the x86
runtime, Node and Microsoft Edge. Build the self-hosted compiler with `npm test` first.

The fixture is an ordinary .NET 10 library. Its exception classes derive from the authentic
`System.Exception`, so every bundle carries the supported 32-bit `System.Runtime` and
`System.Private.CoreLib` (decision 0044). The x86 CLR runs the same DLL as the oracle. The
self-hosted compiler then compiles each kernel in Node and in Edge and must return the CLR's value.

The kernels cover:

- a typed catch
- derived-before-base ordering
- a base catch receiving a derived exception
- a mismatch reaching an outer handler
- `catch (Exception)`
- rethrow preserving the type
- finally ordering
- a null variable of the exception class type (NullReferenceException on the CLR)
- one `catch (Exception)` handler first receiving a divide-by-zero and then an object, rethrowing both
- same-named exception classes in two assemblies staying distinct
- framework exceptions built with their parameterless constructors, whose default messages come from CoreLib `SR` resources folded at compile time (#304)

`Filtered` uses an exception filter and must return the explicit -2 diagnostic at its handler offset.
`Uncaught` must reach the host on the exported object tag. The design is decision 0054.
