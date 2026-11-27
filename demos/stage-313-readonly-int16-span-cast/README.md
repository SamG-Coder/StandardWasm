# Stage 313 · Readonly signed Int16 span views

The actual MemoryMarshal.Cast from a readonly Char span to a readonly Int16 span retains the original packed owner, offset and length. Its indexed read sign-extends the selected 16 bits and uses the ordinary span range check. Updates to the original array remain visible through the view.

The CLR, Node and Edge fixture checks signed values, aliased updates, empty spans and bounds. The separate writable overload retains its explicit compiler diagnostic.
