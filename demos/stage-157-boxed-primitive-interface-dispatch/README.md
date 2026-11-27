# Stage 157 · Boxed primitive interface dispatch

Build the self-hosted compiler with `npm test`, then run
`npm run demo -- stage-157-boxed-primitive-interface-dispatch` on Windows
with the .NET 10 SDK, the x86 runtime, Node and Microsoft Edge.

The runner compiles ordinary C# calls through CoreLib's `ISpanFormattable`.
It compares Node and Edge against the x86 CLR using the same fixture DLL.
The cases verify exact interface membership, identity of a successful cast,
and both buffer sizes for the original explicit `Char.TryFormat` body.
A managed reference implementation exercises the same call site and checks
its `out` value and writes to the slice.
The runner also passes runtime `Type` descriptors through the original
`IFormatProvider.GetFormat` declaration and through two virtual overrides.
The override cases pass two descriptors and a null argument.
The byte-span cases exercise implicit conversion and construction from a
managed array, including aliasing, slicing, a returned view, null, empty views
and bounds. Writable character span cases get references through CoreLib's
`MemoryMarshal.GetReference` and verify writes to sliced array storage,
including a generic caller. Dereferencing the default span's reference
must raise the same typed exception as on the CLR.

The sealed string receiver cases compile the original CoreLib bodies for calls
through the Object slot and a String-declared virtual slot. They compare
returned string identity and the typed null-receiver exception.

The runtime type cases compile CoreLib's unchanged `Exception.GetType` body.
They verify exact descriptors for base and derived exceptions, plus its
typed null-receiver failure.

The display-name cases execute the original base call to
`Object.ToString`. They check names for nested, open and closed generic
types and arrays, UTF-16 names, cached string identity, and a null receiver.

The root dispatch cases call `Object.ToString` at the same virtual site with a
bare object, a string, a class override, an inherited implementation, a
runtime type descriptor, a boxed scalar and a nominal reference array.
They also cover the null receiver and primitive vectors. Each primitive array
allocation has a distinct Wasm GC subtype for its CLR element type. Array
access still uses the shared storage supertype, so widening to `object` does
not copy the array or change its identity. The tests compare `GetType` and
`ToString` for `byte[]`, `bool[]`, `char[]` and `ushort[]`, check writes through
the original typed aliases, and include a data-initialized vector. An export
that accepts storage-typed primitive arrays from the Wasm host still leaves
the runtime type unknown, so affected reflection calls report a diagnostic.

The Type.Name cases share the runtime descriptor cache machinery. They
verify simple names for nested generic types and arrays, typed null
behavior, and the distinction between simple names and display names.

Character span string conversions exercise constrained calls to the platform
Object slot. Writable and read-only spans copy the exact view into an immutable
string. The cases check slicing, unpaired UTF-16 surrogates, supplementary
characters, the default empty view and a stack-backed buffer.

Both `Int32` cases have CLR answers. The compiler now binds the original
`Number.TryFormatInt32<char>` generic method. Until the formatting pipeline
lowers, the verifier accepts only the located diagnostic at the pinned local
signature of `Number.TryNegativeInt32ToDecStr<char>`.
Once the pipeline compiles, the verifier requires the CLR answer.

See decision 0084 and issue #272.
