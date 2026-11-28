# Stage 222 · Explicit type initialization

Run `npm run demo -- stage-222-explicit-type-initialization` with Windows x86
.NET 10, Node and Edge. The runner checks the same .NET Standard DLL in all three
environments and records results in `artifacts/stage-222-explicit-type-initialization/`.

The compiler resolves a proven type token through the normal bundled metadata
and uses the same guarded managed initializer as implicit CLR triggers. The
fixture covers one-time initialization, BeforeFieldInit, closed generic
isolation, reentrant execution, cached failure identity, and a type with no
initializer. Open generic definitions and array types require no call. An
unproved handle receives a located diagnostic.
