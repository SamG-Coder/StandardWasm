# Stage 238 · Entry assembly identity

The fixture uses an ordinary .NET executable and an independent .NET Standard dependency. Run `npm run demo -- stage-238-entry-assembly-descriptors` with a built compiler and the Windows x86 CLR. The browser SDK test project provides authentic trimmed Mono CoreLib and retains the framework's native-width metadata witness.

The Windows oracle expects 941 when the default identity is the entry executable from both assemblies, 947 when the dependency becomes the entry assembly and is then restored, and 953 after an explicit null override is read and restored.

The unchanged standalone Blazor publish now gets through the original managed entry lookup and stops at the routing metadata attribute reader. The standalone parity fixture remains pending because the genuine CoreLib assembly type initializer reaches unrelated unsupported collection operations. It continues to require all three positive values.
