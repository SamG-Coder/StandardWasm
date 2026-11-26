# Stage 83 · Interface references

Run `npm run demo -- stage-83-interface-references` and `npm test`.
The two .NET Standard assemblies pass an object through a linked interface field,
constructor parameter and return value. The generated code must preserve the
original reference across interface inheritance, class inheritance and null
stores. The demo returns 5.

The tests also cover an invalid assignment from a structurally identical class.
Interface dispatch, casts to interface types and generic interface references
need additional compiler support. Interface parameters are currently limited
to managed calls so JavaScript callers cannot bypass nominal validation.
