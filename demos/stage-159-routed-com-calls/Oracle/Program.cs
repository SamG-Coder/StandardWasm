using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using ComCallFixture;

internal static unsafe class Program
{
    private const int FailureCode = unchecked((int)0x80004005);
    private static nint expectedReceiver;
    private static int calls;

    private static void Main(string[] args)
    {
        void** vtable = stackalloc void*[4];
        var site = new Site { VTable = vtable };
        expectedReceiver = (nint)(&site);
        vtable[3] = (delegate* unmanaged[Stdcall]<Site*, int>)&ScalarNotify;
        int scalar = Calls.Scalar(expectedReceiver);
        vtable[3] = (delegate* unmanaged[Stdcall]<Site*, Result>)&WrappedNotify;
        int wrapped = Calls.Wrapped(expectedReceiver);
        if (scalar != 0 || wrapped != FailureCode || calls != 2)
            throw new InvalidOperationException("The CLR vtable did not dispatch both calls with the original receiver.");
        File.WriteAllText(args[0], JsonSerializer.Serialize(new
        {
            runtime = Environment.Version.ToString(),
            architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            assembly = typeof(Calls).Assembly.Location,
            scalarRow = typeof(Calls).GetMethod(nameof(Calls.Scalar))!.MetadataToken & 0xffffff,
            wrappedRow = typeof(Calls).GetMethod(nameof(Calls.Wrapped))!.MetadataToken & 0xffffff,
            scalar,
            wrapped,
            calls
        }));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int ScalarNotify(Site* receiver)
    {
        if ((nint)receiver != expectedReceiver) return -1;
        calls++;
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static Result WrappedNotify(Site* receiver)
    {
        if ((nint)receiver != expectedReceiver) return new Result(-1);
        calls++;
        return new Result(FailureCode);
    }
}