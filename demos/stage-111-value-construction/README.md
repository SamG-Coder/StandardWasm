# Stage 111 · Value-type construction

Run `npm run demo -- stage-111-value-construction`. The linked demo returns
16610. The full CLR, Node and Edge suite runs with `npm test`.

The fixture constructs a local value type (`Pair`) and a value type from a
separately built dependency (`Extent`, shaped like `System.Drawing.Size`). It
compares these against the CLR:

- defaults left by a `: this()` constructor
- field writes, including a read-modify-write of `this`
- copy independence after reassigning a local or a class field
- chained `this(...)` constructors, local and linked
- `newobj` results passed as arguments, and `newobj` inside a `try`
- a reference constructor that embeds a linked value (`Panel`)

Roslyn constructs into locals in place (`ldloca; call .ctor`), so both the
`newobj` form and the in-place form are exercised. The lowering is described
in [decision 0042](../../docs/decisions/0042-value-type-construction.md).
A constructor that calls an instance method on its receiver, and a `ref`
argument to an ordinary method, are diagnosed at the call's IL offset.
