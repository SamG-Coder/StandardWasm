# Stage 274 · Runtime assembly equality

The compiler keeps the original CoreLib `Assembly.op_Equality` body.
Its virtual `Object.Equals(object)` call compares reference identity
when both operands are represented by canonical runtime Assembly
descriptors. The operator's original same-reference and null branches
still run. The lowered virtual call retains its receiver null check.

Ordinary subclasses can override `Equals`. The compiler keeps its
existing located diagnostic when a subclass would need to enter the
canonical descriptor representation. The fixture confirms the override
executes on the CLR for two distinct subclass objects.

Run the same .NET Standard 2.0 DLL on Windows x86 CLR, Node and Edge with
a frozen compiler built by `npm test`.

    npm run demo -- stage-274-runtime-assembly-equality

The Windows oracle uses the installed x86 CLR. Node and Edge link the
genuine browser Mono CoreLib to execute the original operators. Set
`STANDARDWASM_MONO_CORELIB` when the matching runtime package is outside
the NuGet cache. Cases cover the same assembly reached through two
producers, two different assemblies, equality and inequality, and all
null operand combinations.
