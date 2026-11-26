# Stage 120 · Virtual calls on newly allocated objects

Run `npm run demo -- stage-120-allocated-virtual-receivers`. The compiler
runs an ordinary .NET Standard object initializer using a virtual setter on
an unsealed class and then reads the value through its original virtual
method. For input 5 the result is 25.

`npm test` compares those original IL methods with the CLR in Node and
Microsoft Edge. It also covers override selection, two paths that allocate
the same class, an unknown argument, a mixed join and values replaced by a
method, field or array load. Rejected calls report their IL offset and
leave the output buffer unchanged. See [decision 0048](../../docs/decisions/0048-allocated-virtual-receivers.md).
