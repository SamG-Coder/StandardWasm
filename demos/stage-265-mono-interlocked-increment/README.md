# Stage 265 · Mono Interlocked increment

The browser compiler supplies a body for the exact static Mono CoreLib
Interlocked.Increment(ref int) InternalCall. It checks the CoreLib identity,
method name, signature, runtime flag and absence of IL. Existing managed
implementations retain their original path.

The body reads and writes the same traced Int32 location used by managed
code. It stores the wrapped 32-bit increment and returns the new value. A
null location raises NullReferenceException. With one managed execution
agent per guest, the read, arithmetic and write run synchronously without
host calls or suspension. Shared-memory or multi-agent execution requires
a separate atomic implementation.

Run the ordinary .NET 10 fixture on Windows x86 CLR, Node and Edge
with a frozen compiler built by npm test.

    npm run demo -- stage-265-mono-interlocked-increment

The Windows x86 run uses its installed CLR as the oracle. Set
STANDARDWASM_MONO_CORELIB to the genuine browser Mono CoreLib DLL when the
matching runtime pack is outside the normal NuGet cache. The Node and Edge
runs link the same fixture DLL against that same browser Mono CoreLib.

The fixture exercises a local alias, static wraparound, a projected field,
an array element, a null receiver, and a null location from Unsafe.NullRef. Two scheduler
instances execute the original TaskScheduler.Id getter and observe
successive IDs and stable reads on each instance.
