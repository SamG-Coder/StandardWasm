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
A constructor that calls a mutating instance method on its receiver is
diagnosed at that method's write through `this`. Read-only methods are
admitted by Stage 121 and decision 0049. A `ref` argument to an ordinary
method is diagnosed at the call's IL offset.

`DisposeScoped` covers a readonly `ref struct` with a managed reference and a
native unsigned integer. It checks disposal on normal exit and overflow before the outer
catch reads the shared state. This uses the managed value ABI. Ref-like
types containing managed byrefs, pointers or nested values still report a
diagnostic, as do fields, arrays and generic specializations containing a
ref-like value.
