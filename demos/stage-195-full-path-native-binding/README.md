# Stage 195 · Compiled Windows full-path binding

Compile a normal .NET 10 DLL with the raw pointer declaration used by CoreLib
for `kernel32.dll!GetFullPathNameW`. The generated Wasm imports guest memory
and the four-argument browser host function.

Run `npm run demo -- stage-195-full-path-native-binding`. Node and Edge check
the argument order and result using a recording import, including a null
destination capacity query and a UTF-16 write with the optional file-part
pointer. The fixture checks the compiler boundary. Windows path normalization
and CoreLib's managed byref pinning are separate checks.
