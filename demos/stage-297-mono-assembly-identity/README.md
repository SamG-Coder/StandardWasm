# Stage 297 · Canonical Mono assembly identity boundary

Canonical Assembly receivers execute the original Mono `GetName()` forwarding body and `GetName(bool)` override. The authenticated runtime handle is an instance-local module slot. Browser assemblies loaded from bytes supply the verified null CodeBase. The genuine `AssemblyName.Create` path still requires its native `GetNativeName` leaf and the original fixed-buffer carrier. That diagnostic remains located at the native method.

Run `npm run demo -- stage-297-mono-assembly-identity` with a frozen `STANDARDWASM_COMPILER_PATH`. The fixture confirms both overloads reach the native boundary while an independent managed `AssemblyName` maintains its own fields and authored virtual overrides remain callable. Set `STANDARDWASM_MONO_CORELIB` to select an installed browser Mono metadata image if the version differs from Windows CLR.
