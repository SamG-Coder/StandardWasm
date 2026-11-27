# Stage 195 · Compiled Windows full-path binding

Compile a normal .NET 10 DLL with the raw pointer declaration used by CoreLib
for `kernel32.dll!GetFullPathNameW`. The generated Wasm imports guest memory
and the four-argument browser host function.

Run `npm run demo -- stage-195-full-path-native-binding`. Node and Edge check
the argument order and result using a recording import, including a null
destination capacity query and a UTF-16 write with the optional file-part
pointer. The compiled relative-path fixture also uses the production browser
host, guest stack buffers, and an explicit virtual current directory. It
checks the resulting path and the returned file-part address. Stage 194
compares the host's path normalization with Windows x86. CoreLib's managed
byref pinning is a separate compiler step.
