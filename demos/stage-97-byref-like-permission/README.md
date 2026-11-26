# Stage 97 · Generic byref-like permission

Run `npm run demo -- stage-97-byref-like-permission` on the .NET 10 SDK
and Node. The compiler builds and runs two linked .NET 10 libraries.
The fixture returns 23 through an interface with the CLI
`AllowByRefLike` flag and ordinary scalar, class and struct arguments.

The CLR comparison in `npm test` covers ordinary argument defaults,
rejects a field reference with a different closed interface identity,
and checks the explicit diagnostic for an actual byref-like struct
argument. The latter is legal on the CLR. StandardWasm still requires
byref-like lifetime analysis and managed address lowering for that case.
Generic interface dispatch is separate.

See the [runtime flag definition](https://github.com/dotnet/runtime/blob/v10.0.10/src/libraries/System.Private.CoreLib/src/System/Reflection/GenericParameterAttributes.cs)
and the [primitive generic layout decision](../../docs/decisions/0036-primitive-generic-layouts.md).
