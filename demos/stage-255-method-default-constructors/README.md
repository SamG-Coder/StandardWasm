# Stage 255. Constructor constraints on generic method arguments

Run `npm run demo -- stage-255-method-default-constructors`. The same
.NET Standard methods run on Windows x86 CLR, Node and Edge. The fixture
covers an overloaded constructor, a closed generic class, an interface
bound alongside the constructor, System.Object, a struct and an Int32.

The compiler verifies a closed reference type's own public parameterless
instance constructor from metadata. Value types satisfy the CLR default
constructor constraint. The managed body remains unchanged. This stage
implements the constraint on a method specialization.
