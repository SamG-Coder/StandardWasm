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
admitted by Stage 121 and decision 0049. `BorrowExtent` verifies that an
ordinary linked `ref` call updates the caller's sequential struct.

`DisposeScoped` covers a readonly `ref struct` with a managed reference and a
native unsigned integer. It checks disposal on normal exit and overflow before the outer
catch reads the shared state. This uses the managed value ABI. Ref-like
types containing managed byrefs, pointers or nested values still report a
diagnostic, as do fields, arrays and generic specializations containing a
ref-like value.

Run `node demos/stage-111-value-construction/verify-constrained.mjs` for the
constrained override check. It uses matching .NET 10 runtimes including win-x86
and compares the same .NET Standard DLL on the CLR, Node and Edge. The
override updates the original location and preserves an earlier value copy.
The check also covers the boxing diagnostic and equality and inequality
branches on two closed generic instantiations. It also compiles the original
CoreLib debugger getter and compares the detached result with the CLR.

The bucket fixture saves a returned `ref int` in a managed local, rebinds an
alias, and writes through it from another .NET Standard assembly. It compares
array values, full-width arithmetic and typed null and bounds failures. It
also checks the diagnostic for an exported managed address.

The struct-array fixture takes two references to one closed generic element.
It checks default slots, value copies, direct array stores, writes through
aliases, and typed failures from taking an element address.

The primitive descriptor fixture compares closed `typeof(T)` against the
canonical primitive types, including pairs with shared Wasm storage. It also
checks the `IsValueType` branch on both a primitive and a reference type.

The closed interface fixture checks implicit and explicit implementations,
including different `int` and `uint` instantiations on one class, a closed
generic implementer, polymorphic calls, and a typed null receiver failure.

The primitive hash fixture invokes the original CoreLib `Int32` and `UInt32`
`GetHashCode` overrides through constrained generic calls. It covers signed
and unsigned values and reading the current `Int32` from an array reference.
