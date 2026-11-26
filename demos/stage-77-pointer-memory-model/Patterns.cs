using System;

namespace PointerPatterns
{
    // Pointer and address patterns that upstream WinForms relies on (decision 0005/0024 audit: unsafe in 419 of
    // 2,233 source files). Each scenario returns an int so every candidate memory model can be compared with the CLR.
    public static unsafe class Scenarios
    {
        // fixed over a byte[]: the pointer reads the array's own storage.
        public static int FixedSum(int length)
        {
            var data = Fill(length);
            int sum = 0;
            fixed (byte* p = data)
            {
                for (int i = 0; i < length; i++) sum += p[i];
            }
            return sum;
        }

        // Aliasing inside one fixed scope. The CLR guarantees that p and data name the same bytes, so writes through
        // either are immediately visible through the other. A model that copies the array for the fixed scope fails.
        public static int FixedAliasing(int seed)
        {
            var data = new byte[4];
            int seen;
            fixed (byte* p = data)
            {
                data[0] = (byte)seed;       // managed write, pointer read
                seen = p[0];
                p[1] = (byte)(seed + 1);    // pointer write, managed read
                seen = seen * 1000 + data[1];
            }
            return seen * 10 + data[1] - data[0];
        }

        // A pointer escapes into a callee that writes through it (CsWin32-style out parameters).
        public static int PointerIntoCallee(int value)
        {
            var data = new int[3];
            fixed (int* p = data) WriteTriple(p + 1, value);
            return data[0] * 100 + data[1] * 10 + data[2];
        }

        private static void WriteTriple(int* target, int value)
        {
            target[-1] = value;
            target[0] = value + 1;
            target[1] = value + 2;
        }

        // fixed over a string reads its UTF-16 code units in place.
        public static int FixedString(int repeat)
        {
            string text = new string('W', repeat) + "inForms";
            int sum = 0;
            fixed (char* c = text)
            {
                for (int i = 0; i < text.Length; i++) sum += c[i];
            }
            return sum;
        }

        // Span slices alias the array they view.
        public static int SpanSlice(int seed)
        {
            var data = new byte[8];
            Span<byte> window = data.AsSpan(2, 3);
            window[0] = (byte)seed;
            window[2] = (byte)(seed * 2);
            return data[2] * 1000 + data[4] + window.Length;
        }

        // stackalloc into a Span, as WinForms does for small native buffers.
        public static int StackBuffer(int count)
        {
            Span<int> buffer = stackalloc int[16];
            for (int i = 0; i < buffer.Length; i++) buffer[i] = i * count;
            int sum = 0;
            foreach (int item in buffer) sum += item;
            return sum;
        }

        // The address of a struct local (RECT/POINT out parameters in interop).
        public static int LocalStructAddress(int x)
        {
            Point point = default;
            Point* p = &point;
            p->X = x;
            p->Y = x * 2;
            Mutate(&point);
            return point.X * 100 + point.Y;
        }

        private static void Mutate(Point* p) => p->Y += 1;

        // The address of an array element of struct type, the pattern behind SendMessage(ref RECT).
        public static int StructArrayElement(int x)
        {
            var points = new Point[2];
            fixed (Point* p = &points[1])
            {
                p->X = x;
                p->Y = x + 5;
            }
            return points[1].X * 100 + points[1].Y;
        }

        internal static byte[] Fill(int length)
        {
            var data = new byte[length];
            for (int i = 0; i < length; i++) data[i] = (byte)(i * 7 + 3);
            return data;
        }

        public struct Point
        {
            public int X;
            public int Y;
        }
    }
}
