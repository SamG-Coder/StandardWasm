# Stage 108 · BeforeFieldInit method calls

Run `npm run demo -- stage-108-beforefieldinit-calls`. The linked demo returns
11. The full CLR, Node and Edge suite runs with `npm test`.

Methods and constructors on a BeforeFieldInit type can run before the first
access to one of its static fields. The fixture observes this timing for
local and linked static calls, a constructor and an instance call.
Since [Stage 129](../stage-129-type-initializers/README.md), a reached static
field and a static call that needs a precise initializer run that initializer
first. A direct attempt to export the initializer is still diagnosed.

This uses the timing allowed by [ECMA-335](https://ecma-international.org/publications-and-standards/standards/ecma-335/)
Partition I 8.9.5. Initializer execution, including failure and recursion, is
decision 0057.
