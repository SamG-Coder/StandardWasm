# 0064 · Specialize closed generic methods

Status: proposed for Stage 136.

## Context

The original CoreLib resource-stream code calls
`ArgumentOutOfRangeException.ThrowIfNegative<long>`. Its IL operand is
a MethodSpec. Stage 105 already specializes code by a closed declaring
type. A method on that type can also declare and bind its own type
parameters, including when it recursively calls another instantiation.

## Approach

Read the MethodDefOrRef target and bounded instantiation arguments from
the MethodSpec table. Resolve the method within the caller's assembly
bundle and apply the target's access and generic constraint rules.
A compiled body is identified by its assembly, MethodDef, closed
declaring type and exact method type argument vector. Reserve the body
before lowering calls so recursion reuses the same specialization.
Two instantiations can share a storage type and still require distinct
CLR type identities.

Keep declaring-type parameters (`!n`) and method parameters (`!!n`)
in distinct contexts when decoding signatures, locals and nested calls.
Metadata reads keep the encoded argument slice available until the
caller context is known. Unsupported argument forms and constraints
carry the existing located compiler diagnostic.

## Current verification

The first checkpoint compares MethodSpec targets, arities and argument
bytes with the CLR metadata reader for ordinary .NET Standard DLLs
and the installed CoreLib images in Node and Edge. This covers both
MethodDef and MemberRef targets, and argument slices containing the
declaring and method parameter encodings. Subsequent checkpoints will
test execution, including recursion and type-argument-dependent locals.
