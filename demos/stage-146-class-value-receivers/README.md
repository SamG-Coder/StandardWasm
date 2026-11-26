# Stage 146 · Class value-field receivers

Run `npm test` to build the self-hosted compiler, then run
`npm run demo -- stage-146-class-value-receivers` on Windows with the matching
.NET 10 x86 and default CLR runtimes, Node and Microsoft Edge.

The compiler calls ordinary value-type instance methods through a managed
class-field address. The tests check an assignment visible after throwing, a
null receiver checked before argument evaluation, and the original .NET
`BitVector32` section setter preserving an adjacent section. The x86 CLR
validates the section fixture against the official factory before the comparison.

See [decision 0074](../../docs/decisions/0074-managed-class-value-locations.md).
