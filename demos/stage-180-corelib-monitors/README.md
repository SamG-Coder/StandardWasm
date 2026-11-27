# Stage 180 · CoreLib monitors

Compile ordinary .NET Standard monitor operations and the original CoreLib
monitor entry points used by the WinForms resource dependency graph. A
verified local `lockTaken` update uses the browser host's reentrant monitor
for its owning agent.

Run `npm run demo -- stage-180-corelib-monitors` to compare the compiled
fixture in Node and Edge with the Windows x86 CLR.
