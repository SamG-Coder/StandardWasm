# Stage 94 · Floating-point instance fields

Run `npm run demo -- stage-94-floating-point-fields` and `npm test`.

Two ordinary .NET Standard assemblies store `Single` and `Double` fields
through linked references. The demo returns 3 by subtracting the stored
`Single` value, rounded to 16,777,216, from a `Double` value of 16,777,219.

The CLR comparison covers inheritance, value copies, defaults, signed zero,
subnormals, infinity, NaN, null receivers and field signature mismatches.
