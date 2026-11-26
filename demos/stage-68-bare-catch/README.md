# Stage 68 · Bare catch clauses

Run `npm run demo -- stage-68-bare-catch`. The generated Wasm rethrows from a
bare catch to a typed handler and returns -301.

C# emits a catch clause that references the platform `System.Object` type.
The compiler maps it to the supported managed exception tags. This stage
checks regular returns, specific handler ordering, multiple exception kinds
and rethrow with the original type. Engine traps stay outside managed dispatch.
