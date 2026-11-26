# 0075 · Addresses within value constructors

A value constructor can place several copies of `this` on the evaluation stack
while computing a chained assignment. Those copies are addresses of one
location. Reloading its value must see earlier writes through another alias.

Keep the constructor's receiver-in, value-out ABI. Continue reducing simple
non-escaping receiver operations to direct frame accesses. When that reduction
cannot represent a receiver load, track a constructor-frame address in the IL
stack verifier. It can read and write fields of the constructor's own value
and always refers to argument zero. A field write replaces the snapshot in
that frame slot. Subsequent accesses through any outstanding address use that
updated slot. A Wasm placeholder carries no address bits or memory exposure.

These addresses stay within their constructor frame. Passing them to another
method, converting them to native pointers, and treating them as regular
object references remain unsupported. Previously accepted simple constructor
calls keep their existing lowering and failure behavior.
