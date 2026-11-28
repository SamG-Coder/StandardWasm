# Stage 322 Window rectangle queries

Run `npm run demo -- stage-322-window-rect-queries`. The same .NET fixture calls Win32 on the Windows x86 CLR and is compiled to Wasm for Node and Edge. The browser exercises the normal launch host with a hidden framed window and a borderless popup.

The fixture verifies screen-relative window edges, zero-based client coordinates, the moment each rectangle becomes available during window creation, and errors after the handle retires. It also verifies that the host writes exactly four signed words and that the compiler rejects a declaration with the wrong pointee. Browser-only checks cover invalid linear-memory addresses.
