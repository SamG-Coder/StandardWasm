# Stage 03 · Managed byte buffers

Run `npm run demo -- stage-03-byte-buffers` after `npm ci`. The expected result is `RoundTrip(3, 1, 511) = 1023`. A three-byte managed buffer stores the low eight bits of 511 and reads that cell back as unsigned 255. The return value also encodes the buffer length. The full suite checks zero initialization, boundaries, and comparison of CLR failures with Wasm traps.
