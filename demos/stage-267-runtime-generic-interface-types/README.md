# Stage 267. Construct canonical generic interface Type descriptors

Run `npm run demo -- stage-267-runtime-generic-interface-types`. The same
compiled .NET Standard fixture runs on Windows x86 CLR, Node and Edge.

The compiler links the authentic `Type.MakeGenericType` call when the
receiver is a literal, unconstrained, single-parameter interface definition
and the argument is a fresh one-element descriptor vector. The vector can
contain a runtime-selected Type. The factory builds canonical closed types
from reachable argument descriptors, so identity agrees with the original
IL token or a second factory call. Null elements produce the supported CLR
argument-null exception. An authentic Void descriptor produces ArgumentException.

The fixture covers String, distinct signed and unsigned generic arguments,
a primitive array, a nominal enum, different interface definitions, null, and repeated
factory calls without a closed IL token. It also covers literal-only TypeDefs and coexistence with a nominal primitive receiver layout. Unknown descriptor graph entries
reach the unsupported trap. Open arguments, constrained and multi-parameter
definitions, and arbitrary Type[] inputs are separate operations.
