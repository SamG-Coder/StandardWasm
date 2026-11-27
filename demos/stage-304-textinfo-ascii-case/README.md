# Stage 304 · TextInfo ASCII case normalization

Compile and run the original Mono TextInfo.ToLowerAsciiInvariant string method with ordinary C# fixture strings. The verified UTF-16 loop preserves source identity for unchanged strings and replaces uppercase ASCII units in new output. Unicode code units outside the ASCII range pass through unchanged.

The verifier compares Windows x86 CLR, Node and Edge, including the typed null exception. Set STANDARDWASM_MONO_CORELIB to the exact browser runtime metadata when it differs from the installed Windows x86 version.
