# Stage 06 · Method definitions

Run the following from the repository root after installing dependencies.

    npm run demo -- stage-06-method-definitions

The module receives its own DLL. The expected result is HasMethods(MethodLibrary.dll) = 1.

The portable reader finds the compressed #~ stream and checks that the row counts and layouts through MethodDef fit within it. Its layout includes the absolute offset, row width, and string and blob index widths. The demo checks the RVA, implementation flags, method flags, and name and signature heap offsets against .NET MetadataReader.

The reader handles widened simple and coded indexes and widened heaps. Uncompressed metadata, pointer and ENC tables, and extended table headers report an unsupported-layout status. It validates the prefix through MethodDef. Later table payloads, heap entries, and method bodies are future stages.

See [ECMA-335, Partition II, sections 22 and 24](https://www.ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf).
