# Stage 302 · Native UTF-8 decoding

Run the ordinary compiled library through the original Mono Marshal.PtrToStringUTF8 wrapper. The signed byte count remains in the CLR's UTF-8 decode path until StandardWasm supplies its portable string allocation and fills the exact number of UTF-16 units. The original managed wrapper owns its null check and control flow. The native strlen operation stops on the first NUL and the decoder implements default UTF-8 replacement.

The verifier checks the same library and input bytes in Windows x86 CLR, Node and Edge. Supply STANDARDWASM_MONO_CORELIB for the exact stock browser runtime metadata when it differs from the installed Windows x86 runtime version.
