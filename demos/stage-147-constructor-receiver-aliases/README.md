# Stage 147 · Constructor receiver aliases

Run `npm test` to build the self-hosted compiler, then run
`npm run demo -- stage-147-constructor-receiver-aliases` on Windows with matching
.NET 10 x86 and default runtimes, including Windows Desktop, Node and Edge.

The tests compare an ordinary chained value constructor and the original .NET
10 Windows Forms `Padding(int)` constructor against the x86 CLR. They verify
that repeated addresses of the receiver observe the same current value, even
when several addresses stay on the stack during the writes.

See [decision 0075](../../docs/decisions/0075-constructor-receiver-aliases.md).
