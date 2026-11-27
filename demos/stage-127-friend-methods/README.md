# Stage 127 · Friend-assembly member binding

Run `npm run demo -- stage-127-friend-methods` on Windows with the .NET 10 SDK, Node and Microsoft
Edge. Build the self-hosted compiler with `npm test` first.

The fixtures are ordinary netstandard2.0 libraries with fixed identities: `FriendProvider` and
`FriendCaller`. Signed variants use stage 78's public keys.

- `Provider/UnsignedGrant` grants `FriendCaller` by name.
- `Provider/SignedGrant` grants `FriendCaller` with the key A public key.
- `Provider/Ungranted` has the same identity as `Provider/UnsignedGrant`, with no grant.
- `Provider/PublicMembers` exposes the same members publicly, so a caller signed with key B can be
  compiled against it.

The callers make three kinds of call on the provider:

- an `internal static` method
- a `protected internal static` method, from a class that is not a subclass
- an `internal` constructor plus an `internal` instance method

The caller also reads and writes an `internal` instance field and updates a
`protected internal` static field from a non-subclass.

The CLR oracle loads each caller with the provider under test in its own `AssemblyLoadContext`:

| Pair | CLR result | Compiler must return |
| --- | --- | --- |
| Unsigned caller, unsigned grant | values | the same values in Node and Edge |
| Signed caller, signed grant | values | the same values in Node and Edge |
| Unsigned caller, ungranted provider | MethodAccessException or FieldAccessException | -6 at the refused call or -2 at the field access, output untouched |
| Key B caller, key A grant | MethodAccessException or FieldAccessException | -6 at the refused call or -2 at the field access, output untouched |

A derived caller also invokes a protected virtual base implementation explicitly
and a protected nonvirtual instance method. Both return the CLR's results for every
provider, including the ungranted provider and the provider that grants another key.

The design is decision 0055.
