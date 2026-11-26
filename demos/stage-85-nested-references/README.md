# Stage 85 · Nested type references

Run `npm run demo -- stage-85-nested-references` and `npm test`.
The compiler binds types through their enclosing definitions across DLLs.
Two branches declare identical nested names and layouts. The generated module
preserves their different nominal identities and the interface field assignment.
The suite also replaces the original public contract assembly with a facade
whose enclosing types forward to a separate implementation.

The signed friend fixture reaches an internal nested type using the full public
key in its platform InternalsVisibleTo declaration. An altered grant is rejected.
The fixture reuses the public-only keys from Stage 78. The linker compares stored
assembly identities, so signature verification and pack provenance belong to
the host. Protected and private cross-assembly member access still need caller
type context.

See the [nested type binding decision](../../docs/decisions/0028-nested-type-binding.md).
