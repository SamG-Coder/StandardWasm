# Stage 306 · Native byte span to managed array transfer

The compiler translates the verified byte-span copy in the original Mono Marshal.Copy path to a bounded transfer into the actual managed array. Existing aliases observe the new bytes. The unaffected prefix and suffix keep their original values.

Run the verifier with STANDARDWASM_COMPILER_PATH set to the matching compiler image. The verifier compares its production transfer emitter with Windows x86 CLR in Node and Edge for valid ranges, empty extents and out-of-range destinations. An invalid native extent must trap before any destination write. It also links the ordinary Marshal.Copy DLL through the original upstream null and negative checks. The full wrapper currently diagnoses String.JoinCore in the exception-message path, so those null cases run only on the stock runtime oracle.
