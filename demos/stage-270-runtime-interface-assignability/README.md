# Stage 270 assignability to factory-created interfaces

The same ordinary compiled library runs against the Windows x86 CLR and through StandardWasm in Node and Edge. It checks `Type.IsAssignableTo` for interfaces created by `MakeGenericType`, including an inherited interface, a class with no matching edge, distinct signed and unsigned type arguments, a boxed struct, and an enum. Null receivers and targets follow CLR behavior. Variant interfaces need a separate proof and retain a diagnostic. The Wasm bundle omits a separate bridge assembly. A remaining exact edge still proves a positive result. The incomplete graph cannot prove a negative result for another closed interface, so both engines confirm the explicit trap.

Run `npm run demo -- stage-270-runtime-interface-assignability` with the Windows x86 runtime available.
