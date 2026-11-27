# Stage 358 · Volatile reference-field addresses

The linker can return the address of a volatile reference field in a value type, as the original `ConcurrentDictionary<TKey,TValue>.GetBucketAndLock` does for its `VolatileNode[]` buckets. The emitted managed reference retains the array owner and element index. Taking the address performs neither a load nor a store, and the existing single-agent threading model is unchanged.

Run `npm run demo -- stage-358-volatile-reference-field-address --no-rebuild`. The same .NET Standard assembly compares the alias after a bucket replacement, the untouched neighbor and value snapshot, default nulls, and writes through the returned reference on Windows x86 CLR, Node and Edge. An address to a volatile scalar field in a value type keeps its existing compiler diagnostic.
