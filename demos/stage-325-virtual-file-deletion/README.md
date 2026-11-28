# Stage 325 Virtual file deletion

Run `npm run demo -- stage-325-virtual-file-deletion`. The Windows x86 oracle operates on files it creates in a temporary directory. Node and Edge compile the same fixture and call the production browser host against an instance-local Win32 mount.

The fixture checks path normalization, Unicode, repeat deletion, missing files and directories, and read-only files. The browser additionally checks extended paths, alias removal shared with long-path lookup, independent launch state, checked memory, and rejection of a declaration with the wrong UTF-16 pointee.

The mount models independent, unopened file entries. Open-file sharing, symbolic links and persistent browser storage have separate contracts.
