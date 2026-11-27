# 0089 Â· Typed calls to dynamically resolved native exports

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
own registry, and the temp-path provider requires a configured filesystem mount. Resolve the
original QCall lookup before enabling the route for the upstream CoreLib path.

For a fixed character reference proven to feed one indirect call without
escaping, the compiler keeps the traced location through argument evaluation.
A linear-memory address is passed directly. A managed array suffix is copied
to the shadow stack and written back to its original offset after the call.
The compiler publishes the original owner as a root across the call. The
shadow-stack frame unwinds on host failure. Crossing a managed helper or an
unproven pointer lifetime retains the located compiler diagnostic.

The scoped import appends `:scoped-utf16` and adds one i32 ABI parameter.
It carries the available UTF-16 units for a traced array suffix, or -1
when there is no traced extent. Providers must opt in with a separate
`scopedUtf16` callback. It must finish synchronously, respect the extent,
avoid managed re-entry and pointer retention, and report platform errors
before modifying the buffer if it throws. The temp-path provider checks
the destination extent before touching memory.
