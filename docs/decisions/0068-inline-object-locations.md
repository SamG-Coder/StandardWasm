# 0068 · Stable storage for addressable inline object arrays

Give a local of an object-element inline-array type one private Wasm GC vector when the original IL takes a supported managed element location. The vector belongs to the local, and its length comes from the verified CLR inline-array layout. A managed location aliases that vector and index throughout the frame.

An ordinary local load snapshots the vector into the existing immutable value aggregate. A store, field update or initialization writes into the existing vector. This preserves independently copied values and existing aliases to the destination. Unmarked locals and other values continue to use the aggregate representation directly.

Recognize the C# compiler's generic inline element helper for an in-range constant index. Verify the MethodSpec arguments, target signature and body, and the exact platform Unsafe.As and Unsafe.Add overloads and argument ordering. Translate the resulting traced location using decision 0067. Support for the original CoreLib span helper, dynamic indices, other element types and nonlocal storage remains under issue #273.
