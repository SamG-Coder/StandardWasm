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
The returned-location fixture carries a struct array element through a
closed generic method in another assembly. It also borrows a reference
array element, checks writes through returned aliases and earlier value
copies, and covers typed bounds errors and exported byref rejection.

The struct-array fixture takes two references to one closed generic element.
It checks default slots, value copies, direct array stores, writes through
aliases, and typed failures from taking an element address.
The interior field test initializes reference, aggregate and Int64 fields
through their managed addresses. Both aliases observe the replacement,
while the earlier value snapshot and neighbouring array slot keep their
contents. A value constructor also initializes a readonly aggregate field.

The primitive descriptor fixture compares closed `typeof(T)` against the
canonical primitive types, including pairs with shared Wasm storage. It also
checks the `IsValueType` branch on both a primitive and a reference type.
Descriptor results from direct calls on closed generic structs and classes
preserve nulls and the identity of each instantiated type. The fixture
compares a reused getter result and distinguishes `Int32` from `UInt32`.
The virtual descriptor fixture calls the same abstract slot with two closed
generic receivers. It compares the resulting array types after the returned
descriptor, null, and a type token have flowed through the same local.
The static `Int32` fixture checks that direct reads and writes observe the
same storage as a returned reference and two `ref` parameters. It covers
two references to one field, references to distinct fields, and initializers.
The runtime type fixture compares `Object.GetType` for a derived instance,
a plain object, a string, and boxed `Int32` and `UInt32` against the CLR.
It includes an interface receiver, a null receiver, and the identity shared
by `typeof(string)` and `typeof(T)` when `T` is `string`.
The array descriptor fixture compares exact identities for primitive,
enum, reference and jagged vectors. Generic methods return the same
descriptors as their closed type tokens. It also tests `GetType` on a
reference array viewed through `object`.

The closed interface fixture checks implicit and explicit implementations,
including different `int` and `uint` instantiations on one class, a closed
generic implementer, polymorphic calls, and a typed null receiver failure.
The interface cast fixture checks `isinst` and `castclass` against a
nongeneric interface. It covers implementations inherited through a class
and an interface, dispatch after the test, reference identity, null, and
the typed invalid-cast exception.
The CoreLib interface fixture casts through `ICustomFormatter`, invokes the
original interface contract on a managed implementation, and compares success,
null, and typed cast-failure behavior with the CLR.

The primitive hash fixture invokes the original CoreLib `Int32` and `UInt32`
`GetHashCode` overrides through constrained generic calls. It covers signed
and unsigned values and reading the current `Int32` from an array reference.

The closed class fixture dispatches through two generic instantiations and
selects ordinary overrides and hidden slots. It also checks a virtual body
reached through an interface and a typed null-receiver exception.

The .NET Standard 2.1 companion exercises
`RuntimeHelpers.IsReferenceOrContainsReferences<T>()` in direct returns and
inside generic branches. It checks primitive and enum types, arrays and
strings, static reference fields, and nested generic value types with and
without instance references.

The mutable receiver fixture checks ordinary local and array-backed
receivers, a by-value argument copy, an alias passed to the receiver's
method, and a write observed after checked overflow. It also executes
the original `Nullable<int>.HasValue` and `BitVector32` mask setter.
The constrained interface fixture invokes implicit and explicit implementations
of `IDisposable.Dispose` through a generic by-reference argument. It checks
that mutations to a local struct and a closed generic struct in an array remain
visible at their original locations.
