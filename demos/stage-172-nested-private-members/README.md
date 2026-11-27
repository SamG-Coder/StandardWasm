# Stage 172 · Nested private members

Compile an ordinary .NET Standard library that uses private members of an enclosing
generic type from two depths of nested types. The metadata linker verifies the
enclosing chain within the current module before admitting the member reference.

Run `npm run demo -- stage-172-nested-private-members`. The same compiled fixture
returns 41 for the private field read, 0 after a private field write, and
2147483647 for the private method call. The verifier compares the x86 CLR, Node
and Edge.
