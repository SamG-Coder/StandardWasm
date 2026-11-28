# Stage 230 · Browser Mono current thread

Run `npm run demo -- stage-230-mono-current-thread` with Windows x86
.NET 10, Node and Edge. The oracle uses the same .NET Standard DLL on
Windows and with the matching stock Browser Mono CoreLib in WebAssembly.
It finds the installed browser runtime pack through the NuGet package
root. Set `STANDARDWASM_MONO_CORELIB` to select the corresponding PE
assembly explicitly. Publishing a stock .NET 10 standalone browser app
restores the runtime pack.

The compiler binds Mono's authentic `Thread.GetCurrentThread()` InternalCall.
It shares the desktop current-thread allocator, initializes Mono's
managed ID, priority and typed self reference, and leaves CoreLib's TLS
store in place. The peer fixture covers repeated identity, matching
positive managed IDs and synchronization-context changes. Public priority
still reaches the separately unsupported native `Thread.GetState`
call, recorded as a located diagnostic.
