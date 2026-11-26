# Stage 136 · Closed generic methods

The first checkpoint reads MethodSpec records from an ordinary .NET
Standard library and two installed .NET 10 CoreLib images. It checks
the referenced method, generic arity and exact bytes of each argument
signature against the CLR metadata reader. The samples include the
CoreLib range guard reached by the resource-stream compiler.

Run `npm run demo -- stage-136-method-instantiations` with matching
.NET 10 x86 and default-host runtimes and Edge installed. The runner
builds the portable metadata probe and compares the same input DLLs
in Node and Edge. It also compiles and executes closed static methods
referenced by MethodDef or MemberRef tokens, covering recursion,
method-typed locals and arrays, class and struct constraints, and
generic methods on closed declaring types. It verifies overloads with
the same Wasm parameter types and field access through a constructed
type. The fixture also verifies nominal, constructed and recursive
interface constraints. A separate .NET 10 fixture verifies constrained
static dispatch through the original CoreLib Int64 implementation and
through user-defined MethodImpl mappings. The range guard's error path
still needs support for boxing a closed generic value.
