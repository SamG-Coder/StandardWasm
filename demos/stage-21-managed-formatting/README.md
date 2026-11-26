# Stage 21 · Managed label formatting

Run from the repository root after installing dependencies.

    npm run demo -- stage-21-managed-formatting
    npm run test:winforms
    npm test

The standalone Wasm module formats Int32.MinValue and returns 45 for its first code unit, the minus sign. The full suite compares the entire returned string with .NET invariant decimal formatting for zero, decimal boundaries and both ends of Int32. It also compares null and empty concatenation, Unicode and embedded NUL data, and mutation of a char array after a string was constructed from it.

Strings now use an immutable GC struct wrapping private packed UTF-16 storage. The portable compiler supports char[] allocation and indexed access, new string(char[]), and string.Concat(string, string). Both construction operations copy the code units, using Wasm array.copy. Null operands are treated as empty for these overloads. The compiler accepts ldnull in string contexts. Reading Length or a code unit from a null string traps in Wasm, where the CLR throws NullReferenceException. A host can pass null to the supported nullable char[] parameter.

The shared managed formatter produces decimal digits from unsigned magnitude, including for Int32.MinValue. It uses ordinary C# rather than lowering all framework ToString overloads. The Windows form and browser call the same LabelText method. JavaScript decodes the result for drawing and accessibility.

The extern-alias fixture checks that a third-party System.String constructor or Concat call and a third-party System.Char allocation produce a diagnostic before emission.

Other number formats, culture providers, string identity and interning, wider framework APIs, and a general null type across reference kinds remain open.
