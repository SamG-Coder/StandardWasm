# Stage 357 · COM class activation

The compiler recognizes the original x86 `OLE32.dll!CoCreateInstance` declaration using its metadata, calling convention, GUID layout and `void**` output signature. It lowers the call to a typed `standardwasm:com` import. A browser platform can register classes and their interface factories per launch. Successful activation publishes a guest pointer backed by the same typed COM registry used by indirect calls.

Run `npm run demo -- stage-357-com-activation --no-rebuild` after generating the compiler. The runner uses Windows x86 .NET, Node and Microsoft Edge. Set `STANDARDWASM_COMPILER_PATH` to verify a specific generated compiler.

The Windows oracle calls the system Global Interface Table from the same .NET Standard fixture. The browser fixture registers one interface for that class and exercises activation followed by a typed call through its returned vtable. The engines compare the HRESULT and output-pointer effects for activation, unknown classes and interfaces, unsupported class context, aggregation and a null output pointer. They also check launch isolation, the browser response to an invalid guest pointer and compiler rejection of incorrect library and output declarations.

The browser provider implements the missing-cookie behavior used by this fixture. Full Global Interface Table, COM apartment, aggregation and Windows class registry behavior remain separate compatibility work.
