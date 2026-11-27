# Stage 297 · Canonical Mono assembly identity boundary

Canonical Assembly receivers follow the original Mono GetName forwarding body, GetName(bool) override, and AssemblyName.Create. The browser supplies an instance-local native identity record built from the loaded assembly metadata. The upstream FillName body reads that record and follows the authenticated native UTF-8 path from Stage 302.

The three ordinary GetName cases link through the original character out locals and reach the raw character borrow for ordinal string equality. The remaining cases verify independent managed AssemblyName values and authored virtual overrides.

Set STANDARDWASM_COMPILER_PATH to an exact compiler image and run npm run demo -- stage-297-mono-assembly-identity. Set STANDARDWASM_MONO_CORELIB when the exact browser metadata differs from the installed x86 CLR version.
