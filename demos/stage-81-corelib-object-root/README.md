# Stage 81 · CoreLib object root

Build the compiler with `npm test`, then run
`npm run demo -- stage-81-corelib-object-root` with the .NET 10 runtime.

The .NET Standard fixture derives from the real runtime's
`MarshalByRefObject`. The runner reads the installed framework assemblies and
passes them unchanged to the self-hosted compiler in Node and Edge. It verifies
the constructor chain and object identity against the CLR.

CoreLib's `System.Object` definition shares the intrinsic root used by
platform object references and occupies no separate class layout slot.
The compiler lowers its exact same-image empty constructor as a no-op.
Other root methods and broader object behavior remain separate.
Framework files come from the installed runtime and are not committed.
