# Stage 315 Windows thread message queue

Run `npm run demo -- stage-315-thread-message-queue` with the current bootstrap compiler. The verifier compiles the same DLL that calls User32 on the Windows x86 CLR. Node and Edge instantiate the emitted Wasm through the application launcher. To verify a specific compiler, set `STANDARDWASM_COMPILER_PATH` to its Wasm file.

Verify per-launch thread messages, signed and unsigned native-word payloads, FIFO order, filtered peeks with and without removal, last-error capture and preservation, and the seven words written to the x86 `MSG` structure. The fixture checks that an ordinary thread post with the numeric `WM_QUIT` identifier follows the range filter. The browser verifier also checks isolation, invalid buffer handling and a located diagnostic for a mismatched pointer declaration.

The host serves one virtual thread per launch. It currently stores explicit thread posts. The guest's clock origin and cursor position differ from the Windows desktop, so the oracle verifies that the timestamp and point fields are written. Window-targeted posts, synthetic quit, painting, keyboard and mouse messages, and blocking retrieval require additional integration.
