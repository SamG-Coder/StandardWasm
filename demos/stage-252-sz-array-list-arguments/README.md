# Stage 252. SZ-array arguments to generic list interfaces

Run `npm run demo -- stage-252-sz-array-list-arguments`. The Windows x86
CLR, Node and Edge compare passing a struct array to its exact `IList<T>`
and a reference array to its exact `ICollection<T>`. Both receiver methods
observe the original array object. A null list remains null.

The compiler recognizes the authenticated CoreLib interfaces and exact
element identity of supported array layouts. Calling `Count` through the
array's interface remains a located unsupported operation. Other slot
implementations and covariance need separate verification.
