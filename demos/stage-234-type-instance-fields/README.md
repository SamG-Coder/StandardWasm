# Stage 234 · Runtime Type instance fields

Run `npm run demo -- stage-234-type-instance-fields` after building the portable compiler. The runner uses the same ordinary .NET Standard DLL on Windows x86 CLR, Node, and Edge.

The expected values are 811 for identity, 823 for separate instances, 827 for null and default storage, and 831 for a copied struct. A mutable Type field retains its original compilation diagnostic.

When a reachable constructor stores a canonical Type descriptor into a readonly instance field, the compiler selects descriptor storage for that FieldDef and repeats layout and verification. A direct field load at a Type comparison can establish the same demand before the compiler visits its constructor. Every reachable store still has to satisfy the declared descriptor representation. The ordinary struct replacement path keeps value-copy semantics. Taking a managed address into descriptor storage and using explicit layout or inline-array storage remain outside this stage.
