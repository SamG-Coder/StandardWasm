# Stage 05 · CLI metadata streams

Run `npm run demo -- stage-05-metadata-streams` after `npm ci`. The runner passes the built StreamLibrary.dll to the Wasm export. Expected output is `Inspect(StreamLibrary.dll, 32, -1) = 5`. The five streams produced by the fixture build are the metadata tables, strings, user strings, GUIDs and blobs.

The reader first locates the metadata root using the Stage 04 code. MetadataStreams.Read then writes four integers per stream into a caller-owned catalog: absolute file offset and length of the ASCII name, followed by absolute file offset and length of the stream data. The caller can identify the tables by the name #~ or #- and find heaps by their names. The status is the stream count on success, -1 for malformed input, or -2 for insufficient catalog capacity after the outer header has been validated. A failure leaves the caller's catalog untouched. The implementation checks names and range overlap pairwise, so callers should provide capacity appropriate for the number of streams they expect.

The verification suite independently reads the declared ranges with a CLR BinaryReader, cross-checks known heap starts with MetadataReader, and runs the same cases in Node and Edge. It covers bad lengths and counts, missing terminators, duplicate names, overlapping payloads and insufficient output space. This stage returns byte ranges. Table row decoding and heap entry decoding are future stages.

The stream layout follows [ECMA-335, Partition II, section 24.2](https://www.ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf).
