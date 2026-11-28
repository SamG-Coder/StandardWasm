# 0103 · Stage runner build cache, Edge-only parity and parallel lanes

Status: accepted (#321, merged b4bff052 on the owner's direction, 2026-11-28). Follows [0102](0102-concurrent-verification.md).

After #320 cut `npm test` to about 2–3 minutes, the 88 stage runners became the slowest part of every gate: about 18 minutes, run one at a time. The owner directed that the runners verify in Edge only and skip `dotnet` rebuilds when the inputs' sha256 hasn't changed.

## 1. Build cache (`scripts/build-cache.mjs`)

`run("dotnet", ...)` in `scripts/toolchain.mjs` sends every `dotnet build`, `dotnet publish` and `dotnet run` through `planDotnet`. The build is skipped when the sha256 of all inputs equals the stamp from the last successful build. `dotnet run` becomes a cached build plus `run --no-build`.

**Inputs.** All of these go into the hash:

- every file in the project folder, excluding `bin/`, `obj/`, `node_modules/`, `artifacts/`, `TestResults/` and dot-folders
- the same for every transitive `ProjectReference`
- `Compile`, `None`, `Content` and `EmbeddedResource` includes outside the folder (a wildcard hashes its whole folder, a superset of what MSBuild reads)
- `HintPath` files
- `Directory.Build.*`, `Directory.Packages.props`, `global.json` and `NuGet.config` up to the drive root
- the `dotnet --version` SDK
- the exact arguments

**Stamp.** Each output folder (`bin/<Configuration>/` or the `-o` folder) holds one stamp, `.standardwasm-inputs.sha256`. Deleting the outputs therefore deletes the stamp. A build that starts removes the stamp first, so a failed or interrupted build can't be mistaken for a current one. Because the hash includes the arguments, a build into the same folder with other arguments replaces the stamp.

**Invariant: never skip a build whose inputs the hash cannot see.** These are always built, exactly as before:

- A project that uses `$(MSBuildThisFileDirectory)`, `$(OutDir)`, `$(OutputPath)` or `<Import>` is never cached. Such projects copy other builds' outputs. Today that covers `Verify` and stages 80 and 85.
- A `dotnet run` with `-a`, `--arch`, `-r`, `-f`, `--os`, `-o` or `-p:` before `--` is never cached. The cached prebuild keeps only the configuration, so `run --no-build` would read another output folder.
- Only `*.csproj`, `*.fsproj` and `*.vbproj` files are recognised as projects.
- `STANDARDWASM_REBUILD=1` turns the cache off.

**Known gaps.** Floating NuGet versions (the repository pins every version) and build-affecting environment variables aren't hashed. A gate that changes either should set `STANDARDWASM_REBUILD=1`.

**Measured.** A warm incremental `dotnet build` of one fixture takes 1.1–1.4 s; a skipped one takes 0.00–0.10 s. Hashing the driver's 214 source files takes about 20 ms.

## 2. Edge-only managed parity

`verifyManagedDemo` (`scripts/managed-demo-parity.mjs`, 26 stages) compiles and runs every case in Edge and checks the CLR oracle against Edge's results. `STANDARDWASM_RELEASE=1` or `--release` also runs the cases in Node and asserts that the two engines agree, matching `npm run test:release` in 0102. `results.json` records `node: null` for an Edge-only run.

About 40 stage scripts with their own Node and Edge code still verify in both engines. Converting them is follow-up work.

## 3. Parallel lanes (`scripts/runners.mjs`, `npm run test:runners`)

The script runs every demo with a `runner` in `demo.json`, after `npm test` has saved `artifacts/StandardWasm.Compiler.wasm`:

- It builds the compiler driver once first. Every later stage then hits the cache instead of building the same project at the same time.
- Stages that share builds run in two fixed serial chains, each in its own lane:
  - **Executable chain:** stages 72→73→74→110/151/112/198, 95→118, 184→187 and 194. These build, publish or read another stage's outputs.
  - **Bootstrap/WinForms chain:** stages 117, 14, 32, 34, 42, 69, 70, 17, 152 and 160. These reference `demos/stage-13-complete-bootstrap` or share the WinForms profile artifacts.
- Every other stage takes the next free lane: at most 8 in total, by default the logical cores minus 2. `--lanes N` overrides that.
- Each stage has a timeout, 900 s by default (`--timeout`). On timeout its whole process tree is killed (`taskkill /T` on Windows) and the stage is recorded as `FAIL timeout`.
- Logs and `summary.json` go to `artifacts/runners/`. The exit status is 1 if any stage fails.

**Adding a stage.** A new runner stage that builds or reads another stage's projects, or references Stage 13, must be added to the matching chain. Otherwise it may build concurrently with its dependency.

**Measured.** 92/92 runners passed in 387 s on 8 lanes, Edge only (#312 + #321, 20e7710f). The serial loop took about 18 minutes on #320's compiler. The longest chain, bootstrap/WinForms with the 216 s Stage 117 census, now bounds the total.
