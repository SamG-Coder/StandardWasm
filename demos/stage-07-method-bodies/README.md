# Stage 07 · Method bodies

Run the following from the repository root after installing dependencies.

    npm run demo -- stage-07-method-bodies

The Wasm module reads the first MethodDef row in its own DLL and maps its RVA to a file-backed IL body. The expected result is InspectRow(BodyLibrary.dll, 1, -1) = 0.

MethodBodies.Read returns the IL file offset and length, maximum evaluation stack, local signature token, InitLocals flag and presence of extra sections. It accepts tiny and fat headers and bounds the whole header and code range to a single file-backed PE section. Extra sections are flagged for the caller. Their payloads and IL instructions are separate decoding steps.

The suite compares every IL byte and the header fields for three C# methods with .NET PEReader.GetMethodBody, including one method with an exception-handler section. Malformed fixtures exercise missing RVAs, virtual tails, truncated ranges and unsupported formats.

See [ECMA-335, Partition II, section 25.4](https://www.ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf).
