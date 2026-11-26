# Stage 73 · Windows executable intake

Run `npm run demo -- stage-73-executable-intake` on Windows with Node and Edge.
It builds the original Stage 72 publishes, tests byte recovery in Node and
drives the actual file drop UI in Edge. Run `node demos/stage-73-executable-intake/run.mjs --serve`
to use the browser intake yourself.

Drop the original executable and its companion publish files, or select the
whole publish folder. Drop a supported single-file executable on its own.
The files stay local to the browser. The intake reports the managed entry
assembly, entry token and byte hash. Windows native launchers are never executed.

Supported inputs include a managed PE with a MethodDef entry point, a standard
.NET apphost with matching same-stem companions, and an uncompressed version
6.0 .NET single-file bundle. Renamed ordinary apphosts, compressed bundles,
native-only apps and assembly binding are separate follow-ups. This stage
does not start the app.

Bundle layout references: [.NET HostModel bundler](https://github.com/dotnet/runtime/blob/a84d826750643e4e849c510a443f9a86f3752a4d/src/installer/managed/Microsoft.NET.HostModel/Bundle/Bundler.cs),
[manifest](https://github.com/dotnet/runtime/blob/a84d826750643e4e849c510a443f9a86f3752a4d/src/installer/managed/Microsoft.NET.HostModel/Bundle/Manifest.cs)
and [file entries](https://github.com/dotnet/runtime/blob/a84d826750643e4e849c510a443f9a86f3752a4d/src/installer/managed/Microsoft.NET.HostModel/Bundle/FileEntry.cs).
