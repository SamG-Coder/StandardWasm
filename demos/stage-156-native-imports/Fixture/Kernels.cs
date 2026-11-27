using System;
using System.Runtime.InteropServices;

namespace NativeImportFixture
{
    // Blittable scalar imports from real Windows system DLLs. On the CLR they bind to the DLLs;
    // in Wasm each one is a typed import that the JavaScript host registry implements with the
    // same C semantics (Stage 156, issue #274, decision 0083).
    static class Native
    {
        [DllImport("kernel32")] public static extern int MulDiv(int number, int numerator, int denominator);
        [DllImport("kernel32", SetLastError = true)] public static extern void SetLastError(int code);
        [DllImport("ucrtbase")] public static extern int abs(int value);
        [DllImport("ucrtbase")] public static extern long llabs(long value);
        [DllImport("ucrtbase")] public static extern double sqrt(double value);
        [DllImport("ucrtbase")] public static extern float fmaxf(float left, float right);
        [DllImport("ucrtbase")] public static extern float roundf(float value);
        [DllImport("ucrtbase")] public static extern float copysignf(float magnitude, float sign);
        // A string needs marshalling (here UTF-16 into native memory): a located -2 in Wasm.
        [DllImport("kernel32", CharSet = CharSet.Unicode)] public static extern int lstrlenW(string text);
        // Not exported by kernel32: the CLR raises EntryPointNotFoundException at the call.
        [DllImport("kernel32")] public static extern int StandardWasmNoSuchEntry();
        // No such library: the CLR raises DllNotFoundException at the call.
        [DllImport("StandardWasmAbsentLibrary")] public static extern int Anything();
    }

    public static class Kernels
    {
        // MulDiv computes number * numerator / denominator in 64 bits and rounds half away from zero.
        public static int MulDivRounds() => Native.MulDiv(7, 3, 2);
        public static int MulDivNegative() => Native.MulDiv(-7, 3, 2);
        public static int MulDivThird() => Native.MulDiv(1, 1, 3);
        public static int MulDivNegativeHalf() => Native.MulDiv(-3, 1, 2);
        public static int MulDivWide() => Native.MulDiv(int.MaxValue, int.MaxValue, int.MaxValue);
        public static int MulDivOverflow() => Native.MulDiv(int.MaxValue, 2, 1);
        public static int MulDivByZero() => Native.MulDiv(5, 5, 0);

        public static int AbsMinimum() => Native.abs(int.MinValue);
        public static int LongAbs() => (int)(Native.llabs(-5000000000L) / 1000);
        public static int SquareRoot() => (int)(Native.sqrt(2.0) * 1e9);

        // C fmaxf ignores a NaN operand; roundf rounds half away from zero; copysignf keeps -0's sign.
        public static int MaxIgnoresNaN() => (int)Native.fmaxf(float.NaN, 3.5f);
        public static int MaxOfNegatives() => (int)(Native.fmaxf(-0.5f, -2f) * 10);
        public static int RoundHalfAway() => (int)Native.roundf(-2.5f);
        public static int SignOfNegativeZero() => Native.copysignf(3f, -0.0f) < 0 ? 1 : 0;

        // SetLastError = true: the error the callee set is saved for GetLastWin32Error.
        public static int LastError()
        {
            Native.SetLastError(1234);
            return Marshal.GetLastWin32Error();
        }

        public static int StringArgument() => Native.lstrlenW("abc");

        // Each failure must reach its own handler, as on the CLR.
        public static int MissingEntry()
        {
            try { return Native.StandardWasmNoSuchEntry(); }
            catch (DllNotFoundException) { return 8; }
            catch (EntryPointNotFoundException) { return 9; }
        }

        public static int MissingLibrary()
        {
            try { return Native.Anything(); }
            catch (DllNotFoundException) { return 41; }
            catch (EntryPointNotFoundException) { return 42; }
        }
    }
}
