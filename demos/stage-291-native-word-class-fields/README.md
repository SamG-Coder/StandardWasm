# Stage 291 · Managed native-word class fields

Ordinary .NET Standard methods take references to signed native-word fields in class instances. The traced projections preserve the object and field identity, including inherited fields and repeated operations through a saved address. The Wasm32 ABI stores the same 32 bits as the Windows x86 CLR while retaining the CLI native-int byref type.

```text
npm run demo -- stage-291-native-word-class-fields
```

Expected fixture results are 101, 103, 107, 109 and 113. The checks cover the original `Interlocked.Exchange` entry, direct writes through aliases, independent object fields, inherited fields, and the typed exception for a null owner. The WinForms replay uses the same lowering at the original `WeakReference<T>.Create` field address.
