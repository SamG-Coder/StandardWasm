# Stage 108 · BeforeFieldInit method calls

Run `npm run demo -- stage-108-beforefieldinit-calls`. The linked demo returns
11. The full CLR, Node and Edge suite runs with `npm test`.

Methods and constructors on a BeforeFieldInit type can run before the first
access to one of its static fields. The fixture observes this timing for
local and linked static calls, a constructor and an instance call.
The linker diagnoses a reached static field, a static call that requires a
precise initializer and a direct attempt to export the initializer.

This uses the timing allowed by [ECMA-335](https://ecma-international.org/publications-and-standards/standards/ecma-335/)
Partition I 8.9.5. Execution of reached initializers, including failure and
recursive initialization, is tracked in [#232](https://github.com/SamG-Coder/StandardWasm/issues/232).
