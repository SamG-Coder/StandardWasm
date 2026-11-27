# 0067 · Managed locations for object vector elements

An address of an `object[]` element carries a traced owner and an element index in a Wasm GC struct. The address is a compiler storage category distinct from both an object value and a native pointer. Every alias reads and writes the same element in the managed owner's array. Passing an address into a method copies the location and preserves identity.

Validate the receiver and the index at `ldelema`. This preserves the order of exceptions relative to evaluation of later call arguments. The array cannot resize, and the location retains its owner through calls and exception unwinding.

The first producer is `ldelema System.Object` on a statically known `object[]` vector. The consumers are `ldind.ref` and `stind.ref`. Intra-assembly `ref object` parameters and locals can hold the same locations. Managed addresses remain inside compiled code. Nested fields, covariant arrays and other array element types require their own validation and lowering. The traced owner follows [decision 0025](0025-pinnable-memory-model.md).

An addressed reference local or parameter has a one-element cell for the lifetime of the frame. Ordinary loads and stores use that cell, so changes made through `out` or `ref` are immediately visible. The verifier keeps the exact CLI element type of each address and requires it to match across calls. A shared Wasm heap stores the cells. Loads restore the reference representation for the verified element type.

An `Int32` array element uses the same owner and index with a distinct
location type. `ldelema` checks null and bounds, and `ldind.i4` and `stind.i4`
read and write the full-width element. Compiled callers can pass, store and
return this location across assembly boundaries. Exports reject managed
location signatures at the Wasm host boundary.

A vector of supported structs stores a nullable cell at each index. Taking
an address creates the cell on first use. Direct reads return immutable
snapshots, and direct stores update the same cell so existing aliases see the
assignment. The element layout, including closed generic arguments, determines
the vector and location types.
