# Stage 120 · Virtual calls on newly allocated objects

Run `npm run demo -- stage-120-allocated-virtual-receivers`. The compiler
runs an ordinary .NET Standard object initializer using a virtual setter on
an unsealed class and then reads the value through its original virtual
method. For input 5 the result is 25.

`npm test` compares those original IL methods with the CLR in Node and
Microsoft Edge. It also covers override selection and two paths that allocate the same
class. Arguments introduced by the host without a runtime type contract
retain their diagnostic. Stage 122 verifies runtime dispatch for replacement
references returned by a call, field or array load. See [decision 0048](../../docs/decisions/0048-allocated-virtual-receivers.md).
