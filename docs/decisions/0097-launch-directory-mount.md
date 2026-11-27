# 0097 · The launch directory is a long-path mount

Status: proposed (issue #275, Stage 360)

## Context

`GetLongPathNameW` resolves against the explicit `win32FileSystem.longPathMount` (Stage 254). The production launcher passed only `currentDirectory`, so every call in an unchanged application returned ERROR_NOT_SUPPORTED. CoreLib's `PathHelper.TryExpandShortFileName` treats that error as "give up", so its retry walk and errors differed from Windows.

## Decision

`runManagedEntry` builds the default filesystem with `launchFileSystem(selection)` in `demos/stage-74-executable-entry/launch.mjs`:
- `currentDirectory` and the mount `root` are both `C:\StandardWasm\App`, the Stage 198 virtual executable directory.
- The entries are exactly the files the user dropped, at their publish-relative paths, with each parent folder as a directory entry. The files a single-file bundle extracts and the supplied framework assets are not on disk beside a Windows apphost, so they are not entries.
- The running executable and the entry assembly are mapped images. They are marked `readOnly`, so `DeleteFileW` reports ERROR_ACCESS_DENIED.
- Names Windows cannot store are not mounted. When a dropped file and a dropped folder collide, the first one dropped wins.
- The volume has no 8.3 aliases.

A platform-supplied `win32FileSystem` still replaces the default entirely.

The mount is a per-instance snapshot. It is the launch's declared file inventory and grants no access to the user's machine or the server. The compiler's assembly list and the resource host still confer no filesystem membership.

## Host semantics changed with it

The changes are measured on Windows x86 in Stage 360:
- Names match through the Windows single-unit upcase table (`win32-upcase.mjs`).
- Short-shaped components are respelled.
- Short buffers receive Windows' piecewise prefix writes.
- Same-drive paths outside the mount ancestry report file or path not found, instead of not supported.
- `longPathMount.rootDosNames` may declare aliases for the root's components.

As a result, Stage 254 and Stage 325 now expect the Windows errors 3 and 2 for their outside-the-mount rows.
