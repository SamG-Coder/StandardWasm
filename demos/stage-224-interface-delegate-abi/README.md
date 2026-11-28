# Stage 224 · Interface types in delegate signatures

Run `npm run demo -- stage-224-interface-delegate-abi` with the Windows
x86 CLR, Node and Edge. The runner compares the same .NET Standard DLL
in all three environments.

Typed delegate thunks keep the nominal interface in the managed
signature and use the ordinary function's Wasm reference storage.
The fixture covers an instance capture receiving an interface, an
interface return, a null interface argument and a custom delegate TypeDef.
Two instantiations also verify that distinct nominal interface arguments
keep separate generic static storage.
