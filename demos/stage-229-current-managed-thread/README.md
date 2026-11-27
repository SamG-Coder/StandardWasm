# Stage 229 · The original CoreCLR current managed thread

An ordinary .NET 10 library calls the original Windows x86 CoreLib Thread.CurrentThread, ManagedThreadId, Priority and SynchronizationContext members. The fixture compiles one library and loads the installed x86 CoreLib and facades without changing their bytes.

Run the comparison after building the compiler with npm test.

    npm run demo -- stage-229-current-managed-thread

The same compiled fixture runs on Windows x86 CLR, Node and Microsoft Edge. The comparisons cover repeated identity, a positive stable managed ID, Priority.Normal, and a synchronization context update and restore on the original Thread. The browser checks also compare the opaque GC reference returned by two independent Wasm instances. The original ThreadState test runs on Windows and reports an explicit compile diagnostic for the separate native GetThreadState leaf in the browser.

## Runtime boundary

This stage admits the original CoreLib InitializeCurrentThread's scoped ObjectHandleOnStack.Create<Thread> and QCall pair. The compiler retains the reference-type local as a traced managed cell. One typed, lazily initialized Thread object is rooted for each guest instance and written to that same cell. The original managed method stores the resulting reference in its existing ThreadStatic field. The single guest thread uses a positive guest managed ID and the normal priority setting.

The CLR runtime backing word is private to this model. The supported guest thread has no Win32 handle exposed through that field. Other CLR thread native operations, scheduling and multiple guest threads need their own runtime support.
