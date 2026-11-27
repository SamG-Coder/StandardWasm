# 0084 · Boxed primitive interface dispatch

An ordinary CLI interface cast retains the original object reference. StandardWasm
uses nominal type layouts for managed classes and one tagged Wasm GC layout for
primitive boxes. For a supported interface, the compiler reads the selected
CoreLib's InterfaceImpl metadata to determine which primitive tags implement
the interface.

Virtual dispatch discovers reachable primitive boxes alongside managed class
allocations. It resolves the declaration to the original managed method through
the same explicit and implicit interface-slot selection used for classes. At
runtime a primitive arm tests the boxed heap and the exact CLI tag, reads the
payload, and passes a single-field value snapshot under the existing read-only
receiver ABI. This preserves the box identity. The emitter diagnoses writes
that need a managed receiver address. Primitive bodies that immediately read
`this` with a matching scalar `ldind` read that field from the snapshot.

This stage exercises nongeneric contracts implementable by class layouts and
primitive boxes. Interface tests that may need separately represented objects
such as strings, arrays, delegates or reflection descriptors still produce a
diagnostic. Generic interfaces, other value-type boxes, and the generic
CoreLib integer formatting pipeline require additional work.
