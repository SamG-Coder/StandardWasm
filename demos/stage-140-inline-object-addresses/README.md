# Stage 140 · Inline object addresses

Run `npm run demo -- stage-140-inline-object-addresses`. A managed reference to an inline-array element observes default assignment of the entire inline array and returns 17. The original C# compiler emits the generic element helper.

The CLR comparisons cover shared references, default assignment, independent value copies and exceptional unwinding. The compiler uses a traced vector for addressable object inline-array locals and preserves ordinary value copies. See [decision 0068](../../docs/decisions/0068-inline-object-locations.md).
