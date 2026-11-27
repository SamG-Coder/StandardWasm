# 0038 · Framework facade versions

[#236](https://github.com/SamG-Coder/StandardWasm/issues/236) tracks linking .NET
Standard libraries against the framework pack selected by a browser host.

The compiler accepts a reference to netstandard 2.0.0.0 against the selected
netstandard 2.1.0.0 pure facade. It also accepts a System.Runtime 9.0.0.0
reference against the selected System.Runtime 10.0.0.0 pure facade. The
original WinForms dependency graph needs this match for the .NET 9 asset of
System.Text.Encodings.Web. All matches use the binding identity
checks for name, public key and neutral culture. A pure facade has no field
or method definitions of its own and must publish exported types. Resolution
continues through normal type forwarding. Multiple eligible assemblies are
diagnosed as ambiguous. Ordinary dependencies retain exact version matching.

The host supplies the runtime pack. Matching an assembly name and key does
not authenticate the supplied bytes. The version rules apply to these
contracts. Other framework pack selections need their own compatibility evidence.

See [Stage 107](../../demos/stage-107-framework-facades/README.md).
