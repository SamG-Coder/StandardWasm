# Stage 254 · Virtual long-path lookup

The normal executable loader resolves the existing typed GetLongPathNameW import against an explicit browser filesystem mount. A launch can pass the following platform configuration to `instantiateManagedModule` or `runManagedEntry`.

```js
{
    win32FileSystem: {
        currentDirectory: "C:\\StandardWasm\\App",
        longPathMount: {
            root: "C:\\StandardWasm\\App",
            entries: [
                { path: "Long Project Folder", kind: "directory", dosName: "LONGPR~7" },
                { path: "Long Project Folder/Example.document", kind: "file", dosName: "EXAMPL~7.DOC" }
            ]
        }
    }
}
```

The mount is a per-instance snapshot. Entry paths are relative to its root and use forward slashes. Each parent directory must have its own entry. Primary names and explicitly supplied DOS aliases share one case-insensitive namespace within each directory. Names compare through the Windows single-unit upcase table, which Stage 360 checks against `RtlUpcaseUnicodeChar`. An optional `rootDosNames` array gives aliases for the root's own components. The mount root and current directory use canonical absolute DOS paths on the same virtual drive. The file list supplied to the compiler and its resource host confer no filesystem membership.

Ordinary relative, rooted, same-drive and mounted local extended DOS paths keep their input separators. As Windows does, the lookup respells only components whose input is short-shaped, meaning it could be an 8.3 name by length and dots. Such a component takes its stored spelling, or the primary name its alias stands for. Other components keep the caller's spelling (Stage 360). Missing leaf and parent paths return the Windows file-not-found and path-not-found errors. A trailing separator after a file returns the directory error.

The raw ABI counts UTF-16 code units. Success writes the text and one NUL, preserves the raw error, and returns the length excluding NUL. A zero-capacity query or a short buffer returns the required capacity including NUL. A short buffer receives the unterminated leading pieces Windows writes once it meets a short-shaped component (Stage 360). Without such a component, the buffer is untouched. An exact in-place source and destination is supported; any other overlap reports an unsupported operation. The managed P/Invoke wrapper continues to own its saved-error transfer.

The mount admits paths within its namespace. The virtual drive holds only the mount and its root's ancestors. A same-drive path outside them returns the file-not-found or path-not-found error. Unsupported namespaces, parent traversal, noncanonical trailing dot or space components, and an absent mount return ERROR_NOT_SUPPORTED. The lexical GetFullPathNameW provider continues to use currentDirectory.

Run the ordinary .NET 10 fixture with the generated compiler from npm test, or set STANDARDWASM_COMPILER_PATH to a frozen build.

    npm run demo -- stage-254-virtual-long-paths

Windows x86 creates authentic DOS aliases on a disposable test directory. Node and Edge use the same original fixture DLL against the production import resolver. The result compares lengths, errors and the full destination buffer, including exact fit, short and in-place branches, and checks browser namespace isolation and memory validation.
