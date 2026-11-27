# Stage 360 · Launch-directory long paths

Run `npm run demo -- stage-360-launch-long-paths` on Windows with the .NET 10 SDK, the x86 runtime and Microsoft Edge. Use `STANDARDWASM_COMPILER_PATH` to test a specific self-hosted compiler.

Issue #275 found that the production launcher configured no long-path mount, so every `GetLongPathNameW` call returned ERROR_NOT_SUPPORTED. Each launch now mounts the Stage 198 virtual executable directory, `C:\StandardWasm\App`, holding exactly the files that were dropped (see [decision 0097](../../docs/decisions/0097-launch-directory-mount.md)).

## What it compares

The verifier publishes an ordinary .NET 10 console application with a `Data Ω\Wide Ω🙂.txt` content file and runs the same publish in three places:
- on the Windows x86 CLR, started from its own folder
- through `runManagedEntry` in Node
- through the Edge folder input and launch worker

The application raises raw `GetLongPathNameW` calls on 24 rows: relative and absolute, exact, short and zero capacity, case-varied Unicode, the extended prefix, missing leaf and parent, trailing separators, in-place buffers, the mount root's parent and the drive root.

Absolute rows start from `GetFullPathNameW(".")`, so every count and buffer hash is taken relative to the launch folder's own prefix. The prefix itself must repeat the caller's spelling. The exit status folds all rows. Node and Edge also run the fixture's `Observe` entry per row against the production default mount and compare each value with Windows.

The Windows oracle also records three host-level measurements, and the verifier replays each through the production provider in Node and Edge:
- **Aliased ancestors:** the real launch folder spelled with the DOS aliases Windows gave its ancestors (the audit's case 14), against an identical mount that declares those aliases as `rootDosNames`.
- **Naming matrix:** 76 rows on a scratch tree, comparing results, errors and every buffer unit written.
- **Upcase table:** `RtlUpcaseUnicodeChar` for all 65,536 UTF-16 units, compared with `win32-upcase.mjs`.

## Windows semantics this stage measured

- **Respelling.** Only components whose input could be an 8.3 name by shape are looked up and respelled: no leading dot, at most one dot, at most 8 units before it and 3 after, with any characters. Such a component takes its stored spelling, or the long name its alias stands for. Every other component keeps the caller's spelling. So `data ω\WIDE ω🙂.TXT` returns `Data Ω\Wide Ω🙂.txt`, while the audit's `long unicode folder ω` stays lowercase.
- **Case matching** uses the single-unit Windows upcase table, so `ω` matches `Ω`, while `ß` and lone surrogates match only themselves.
- **Short buffers.** Without a short-shaped component, a short buffer is untouched. Otherwise, at each short-shaped component Windows writes everything since its previous write as one piece, then the respelled component, as long as each piece fits in capacity − 1 units. Nothing is NUL-terminated. Text after the last short-shaped component is written only with the complete result.
- **Outside the mount.** The virtual volume holds only the mount and its ancestors. A same-drive path that leaves the ancestor chain returns ERROR_FILE_NOT_FOUND or ERROR_PATH_NOT_FOUND instead of ERROR_NOT_SUPPORTED. Naming an ancestor directly succeeds.

## Limits

- The production volume has no 8.3 aliases, as on a volume with short-name creation disabled. A platform can declare `rootDosNames` and entry `dosName`s.
- In-place `GetFullPathNameW` (source equals destination) misbehaves on Windows, so the fixture passes separate buffers. That case is not modelled.
- The short-shape rule counts UTF-16 units. Double-byte OEM code pages and `.` components inside a short buffer were not measured.
- UNC paths, other drives and `..` components still return ERROR_NOT_SUPPORTED, as Stage 254 documents.
