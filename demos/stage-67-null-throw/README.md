# Stage 67 · Explicit null throws

Run `npm run demo -- stage-67-null-throw`. The generated Wasm observes a
typed null exception after running the managed cleanup and returns 113.

Stack analysis must prove that the operand of `throw` is null. The compiler
emits the same `NullReferenceException` tag used by managed null checks.
The CLR, Node and Edge checks cover normal returns, uncaught throws, catch
across a method call, and cleanup through `finally`. Exception object
construction and throwing other reference values are separate capabilities.
