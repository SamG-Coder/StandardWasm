# Stage 160 · Native receiver demand

Run `npm run demo -- stage-160-native-receiver-demand` with the .NET 10
SDK, the Windows x86 .NET 10 runtime, Node and Microsoft Edge. The verifier
compiles one ordinary .NET Standard assembly and checks exact CLR values and
native compiler bytes in both Wasm engines.

Value-type calls can use a managed location or an address in linear memory.
The linker keeps the traced signature during stack verification. Once it
knows the receiver representation, it emits the body required by each actual
call. Managed calls discovered later can still select the traced body.
Native generic calls retain the original closed method arguments.

The fixture checks three cases:

- A pointer calls a value method that updates its receiver. The traced
  signature keeps an unreferenced index, represented by a three-byte
  unreachable body.
- A later caller uses the same method through an element of a managed array.
  Both native and traced bodies run and update their respective storage.
- A native receiver calls the same generic method with two closed types.
  Its narrow and wide results and both updates match the CLR.

Results are written to `artifacts/stage-160-native-receiver-demand/results.json`.
