# 0067 · Managed locations for object vector elements

An address of an `object[]` element carries a traced owner and an element index in a Wasm GC struct. The address is a compiler storage category distinct from both an object value and a native pointer. Every alias reads and writes the same element in the managed owner's array. Passing an address into a method copies the location and preserves identity.

Validate the receiver and the index at `ldelema`. This preserves the order of exceptions relative to evaluation of later call arguments. The array cannot resize, and the location retains its owner through calls and exception unwinding.

The first producer is `ldelema System.Object` on a statically known `object[]` vector. The first consumers are `ldind.ref` and `stind.ref`. Intra-assembly `ref object` parameters and locals can hold the same locations. Managed addresses remain inside compiled code. Nested fields, address-taken locals, covariant arrays, other element types and span operations require their own validation and lowering before entering this representation. The traced owner follows [decision 0025](0025-pinnable-memory-model.md).
