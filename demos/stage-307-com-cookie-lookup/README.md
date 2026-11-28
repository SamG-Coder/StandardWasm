# Stage 307 · COM cookie lookup dispatch

The fixture calls a stdcall vtable slot with the receiver, an unsigned cookie, an identifier pointer, and a `void**` output. Windows x86 uses the actual Global Interface Table for the live, missing, and revoked cookie cases. A separate unmanaged callback tests the one-word managed result wrapper. Node and Edge run the same compiled fixture against registered callbacks through the browser launcher.

```text
npm run demo -- stage-307-com-cookie-lookup
```

The outcomes preserve the HRESULT, unsigned cookie value, and pointer written through the last argument. The route rejects an incompatible output signature and a receiver registered for another dispatch signature. The browser fixture supplies callbacks for this test. Browser COM creation and full interface-table lifecycle are separate from this compiler and dispatch stage.
