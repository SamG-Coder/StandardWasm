# Stage 250 · Direct Int32 receivers

A direct call to an authentic CoreLib Int32 instance method uses the existing
managed reference cell for `this`. The ordinary stack verifier checks the
receiver and arguments before emission. The selected runtime supplies the
method body, including its `ldind.i4` read of the receiver. The existing
nominal function keys continue to cover specialized boxed dispatch.

The same .NET Standard fixture runs on Windows x86 CLR, Node and Edge. It
checks signed comparisons at both integer limits, equality, hash code,
aliasing a class field, calling on array element addresses and retaining a
boxed copy while the source local changes.

Run the stage with the generated compiler.

```sh
npm run demo -- stage-250-direct-int32-receiver
```

Set `STANDARDWASM_MONO_CORELIB` to the selected browser runtime's CoreLib PE or
Webcil image to test the original Mono method bodies against the same CLR
oracle. Results are written to `artifacts/stage-250-direct-int32-receiver/results.json`.
