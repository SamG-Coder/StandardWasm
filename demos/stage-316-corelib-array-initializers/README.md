# Stage 316 CoreLib array initializers

Run `npm run demo -- stage-316-corelib-array-initializers`. The verifier compares the same .NET Standard fixture on the Windows x86 CLR, Node and Edge with the installed x86 .NET 10 framework assemblies.

The fixture calls the original `Path.GetInvalidPathChars` and `Path.GetInvalidFileNameChars`, compares the length and order of their vectors, and checks that mutating the first returned array does not affect the second. It also verifies a separate character array literal compiled from ordinary C# so MemberRef initialization continues to handle 16-bit data, including high code units.
