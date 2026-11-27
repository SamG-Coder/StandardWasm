# Stage 154 · CoreLib resource strings

Run `npm run demo -- stage-154-corelib-resource-strings` on Windows with the .NET 10 SDK, the x86
runtime, Node and Microsoft Edge. Build the self-hosted compiler with `npm test` first.

CoreLib's parameterless exception constructors, such as `new InvalidOperationException()`, take
their default message from an `SR` property. Each property is generated as
`get_X => SR.GetResourceString("X")`, and `GetResourceString` needs `ResourceManager`, which the
compiler cannot build. So until this stage, any CoreLib body that named an `SR` property was
rejected, even on a branch that never runs.

The compiler now reads CoreLib's own embedded `System.Private.CoreLib.Strings.resources` and folds
`SR.GetResourceString("key")` to that key's string. These are the neutral (English) strings the
CLR uses when no satellite resources apply.

The fixture is an ordinary .NET 10 library. The x86 CLR runs it as the oracle. The runner
bundles it with the x86 `System.Runtime` and `System.Private.CoreLib`, and Node and Edge must
return the CLR's value for every kernel. The kernels throw and catch these exceptions, all built
with their parameterless constructors:

- `InvalidOperationException`
- `ArgumentNullException`
- `NotSupportedException`
- `FormatException`
- `KeyNotFoundException`
- `OverflowException`
- `NotImplementedException`
- a sequence caught by different handler kinds

Reading the message back (`e.Message`) needs CoreLib's exception accessors, which are a separate
follow-up. The design is decision 0081.
