# Stage 198 · Virtual executable working directory

Run `npm run demo -- stage-198-virtual-executable-directory` on Windows with
the .NET 10 SDK, x86 runtime, and Microsoft Edge. Use
`STANDARDWASM_COMPILER_PATH` to test a specific self-hosted compiler.

The verifier publishes a .NET console application and runs the same files
on the x86 CLR, through the Node launcher, and through the Edge file input
and launch worker. The application checks that a relative Unicode path
resolves against the working directory and that the filename pointer
addresses the result. It also checks a separate instance with an explicit
platform directory.

The standard launcher uses `C:\\StandardWasm\\App` as its virtual working
directory. File input cannot expose the original Windows directory. This
stage implements path resolution. The virtual path names do not grant access
to the server filesystem or to files on the user's machine.
