# StandardWasm contributor rules

- Preserve names supplied by the user. Use alternative place or project names only on request.
- Accept compiled .NET Standard libraries as input. Keep the browser runtime independent of Blazor.
- Keep metadata loading, IL translation, module emission and browser integration separate. Use descriptive names and small cohesive modules.
- Comment invariants, stack transitions, control-flow assumptions and CLR-to-Wasm semantic differences. Keep straightforward code readable through naming.
- Add an ordinary C# library case for each new compiler capability. Compare the same assembly on .NET and Wasm. Verify browser claims in a real browser.
- Diagnose unsupported operations explicitly. Never silently approximate checked arithmetic, exception handling, initialization or object lifetime.
- Use standardized WebAssembly features where they implement the required semantics. Identify engine requirements and measure performance claims.
- Keep self-hosting dependencies visible. Only mark self-hosting complete after a generated compiler recompiles itself and passes the fixtures.
- Commit source, reproducible build inputs and documentation. Exclude downloaded dependencies, generated modules and build intermediates.
