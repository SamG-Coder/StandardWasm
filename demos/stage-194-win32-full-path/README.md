# Stage 194 · Win32 full-path resolution

Run `npm run demo -- stage-194-win32-full-path` with Windows x86 .NET 10,
Node, and Edge. The verifier compares the original Kernel32 call with the
raw Wasm32 import through the production executable loader. Evidence is
written under `artifacts/stage-194-win32-full-path/`.

The loader accepts an explicit `platform.win32FileSystem.currentDirectory`
such as `D:\virtual\app`. The value must be a canonical DOS directory or
drive root. Each instance uses its own directory. Unconfigured instances
report `ERROR_NOT_SUPPORTED` for this import. The adapter resolves paths
lexically and does not access a real filesystem or contact UNC servers.

The adapter supports ordinary absolute, relative, rooted, and drive-relative
paths, plus UNC paths with a server and share. It resolves dot components
without crossing the drive or share root. A drive-relative path on a
different drive starts at that drive's root. It preserves UTF-16 code units,
the final-separator behaviour and the ordinary trailing space and dot rules.
Expansion of 8.3 aliases remains a separate native API.

`GetFullPathNameW` uses UTF-16 units for both lengths. Success returns the
written length excluding NUL. A short buffer reports Windows' required
capacity, which may include components that are removed during normalization.
The output starts with NUL on short buffers, and the optional file-part slot
receives zero. The bytes after that leading NUL are cleared by this host and
can contain an intermediate prefix on Windows. On success the slot receives
a pointer into the output or zero for a path with no file part. Bounds are
checked before writing either output. The raw import preserves the system
error on success and short buffers and leaves the saved P/Invoke slot to the
managed wrapper.

The adapter reports unsupported paths for extended or device namespaces,
DOS device components, alternate streams, server-only UNC, unusual
intermediate trailing dots and spaces, and overlapping input and output
ranges. Remembered working directories for other drives are a separate
filesystem capability.

[GetFullPathNameW](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getfullpathnamew) ·
[Original CoreLib PathHelper](https://raw.githubusercontent.com/dotnet/runtime/v10.0.10/src/libraries/System.Private.CoreLib/src/System/IO/PathHelper.Windows.cs#L64-L84)
