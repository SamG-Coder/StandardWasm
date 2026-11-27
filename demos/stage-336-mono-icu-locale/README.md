# Stage 336 · Mono ICU locale membership

The linker preserves the original Mono IsPredefinedLocale managed wrapper, including its UTF-16 pin and Boolean conversion. The exact CoreLib P/Invoke is resolved through StandardWasm's existing typed native import. The executable host accepts an instance-local ICU provider via `platform.monoLocaleIcu`. Without a configured provider, the same missing-library thunk raises a managed exception.

The provider loads an isolated Wasm sidecar built from the runtime pack's original Microsoft globalization PAL and ICU archives. The chosen ICU data defines membership and fallback, so different published shards can legitimately return different answers. The guest supplies a bounded UTF-16 loan. The host copies only the units that the PAL reads into its own memory.

Use the stock .NET 10.0.10 browser-wasm Mono runtime pack and the matching Windows Emscripten SDK/cache packages (`Microsoft.NET.Runtime.Emscripten.3.1.56.Sdk.win-x64` and `Microsoft.NET.Runtime.Emscripten.3.1.56.Cache.win-x64`, version `10.0.10`). Run [build-icu.ps1](build-icu.ps1) with their `tools`, `tools/emscripten/cache` and Mono `runtimes/browser-wasm/native` paths. Set `STANDARDWASM_ICU_SIDECAR` to the compiled Wasm if it differs from `artifacts/stage336-icu.wasm`.

Set `STANDARDWASM_COMPILER_PATH` to the exact compiler image and run `npm run demo -- stage-336-mono-icu-locale`. The runner compiles the untouched Mono wrapper and an ordinary locale input DLL in Node and Edge, compares sixteen stock Mono outcomes for each ICU profile, compares eight common x86 CLR outcomes, and checks the missing-provider and guest memory boundaries. A separate unchanged Mono framework probe reaches the original CultureData.InitIcuCultureDataCore dependency.

[Microsoft .NET 10.0.10 globalization PAL](https://github.com/dotnet/runtime/blob/v10.0.10/src/native/libs/System.Globalization.Native/pal_locale.c)
