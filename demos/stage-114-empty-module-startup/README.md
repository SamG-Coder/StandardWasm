# Stage 114 · Empty module startup

Run `npm run demo -- stage-114-empty-module-startup` to compile the ordinary
.NET Standard library and check that twice 21 is 42. Run `npm test` for the
CLR, Node and Edge comparisons on the standalone and linked paths.

The CLR emits a module constructor that calls the annotated static helper.
StandardWasm validates both bodies and the helper's declaring type before
omitting an empty startup. The linked fixture calls into the original
library. A second fixture gives the helper's declaring type a real static
constructor, which the CLR runs before the empty helper. StandardWasm keeps
that startup diagnostic.

The proof covers an empty return and a single local call to an empty managed
helper whose owning class needs no type initializer. Other module
initializers retain explicit diagnostics. Production execution of required
initializers remains tracked in [#170](https://github.com/SamG-Coder/StandardWasm/issues/170).
