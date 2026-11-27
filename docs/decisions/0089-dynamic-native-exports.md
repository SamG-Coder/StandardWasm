# 0089 · Typed calls to dynamically resolved native exports

Status is in development on the WinForms startup integration milestone (#232,
#274, #275). Stage 165 verifies the compiler and browser registry together.

CoreLib's Windows temp-directory code resolves one of two exports, caches the
function pointer in a volatile static field, pins its UTF-16 destination, and
uses a platform-default unmanaged `calli`. Preserve each operation in the
upstream method. The browser provides the registered platform exports.

For a platform-default unmanaged signature with a 32-bit unsigned result,
a signed 32-bit argument and a blittable linear-memory pointer, lower the
call to `standardwasm:native-indirect` with entry `platform:u32(i32,p32)`.
The Wasm parameters are the resolved token, the original signed argument,
and the original data pointer. The result is an i32 carrying the unsigned
bits. Signature and stack validation happen before module emission.

The browser registry resolves explicitly registered library and export names
to instance-local handles and typed tokens. The token selects the registered
callback at invocation. Final library release invalidates its tokens.
A separate resource import supplies the linear memory shared by the compiled
code and host provider. No native platform library is loaded by the browser.

Managed function pointers still use the compiler's Wasm table. COM calls use
the explicit COM convention and retain their receiver-specific validation.
The new route does not add default browser services. The test configures its
own registry, and the temp-path provider requires a configured filesystem
mount. Resolve the original QCall lookup and the scoped writable pin before
enabling that route for the upstream CoreLib path.
