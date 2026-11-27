using System;

namespace FixedStringFixture
{
    // Stage 149 (decision 0077): fixed (char* p = s) reads a frame-owned UTF-16 copy of s
    // with a NUL terminator. Every kernel takes a selection so the CLR and Wasm runs pick
    // the same string: null, empty, ASCII, non-ASCII with a surrogate pair, a leading NUL,
    // and a 300-unit string built at run time.
    public static unsafe class Kernels
    {
        private static string Pick(int selection)
        {
            if (selection == 0) return null;
            if (selection == 1) return "";
            if (selection == 2) return "Wasm";
            if (selection == 3) return "漢\U0001f642z";
            if (selection == 4) return "\0X";
            var units = new char[300];
            for (int i = 0; i < units.Length; i++) units[i] = (char)('a' + i % 26);
            return new string(units);
        }

        public static int First(int selection)
        {
            fixed (char* p = Pick(selection))
                return p == null ? -1 : p[0];
        }

        // Every code unit, weighted by position, so order and width both matter.
        public static int Weighted(int selection)
        {
            string s = Pick(selection);
            if (s == null) return -1;
            int total = 0;
            fixed (char* p = s)
                for (int i = 0; i < s.Length; i++) total += (i + 1) * p[i];
            return total;
        }

        // The CLR guarantees a NUL after the last code unit.
        public static int Terminator(int selection)
        {
            string s = Pick(selection);
            if (s == null) return -1;
            fixed (char* p = s)
                return p[s.Length];
        }

        // The pointer goes to a managed callee, which scans to the terminator.
        public static int Measure(int selection)
        {
            fixed (char* p = Pick(selection))
                return p == null ? -1 : Scan(p);
        }

        private static int Scan(char* text)
        {
            int length = 0;
            while (text[length] != '\0') length++;
            return length;
        }

        // Two strings pinned in one frame, compared as ordinal code units.
        public static int Compare(int left, int right)
        {
            string a = Pick(left), b = Pick(right);
            if (a == null || b == null) return -9;
            fixed (char* x = a)
            fixed (char* y = b)
            {
                int i = 0;
                while (i < a.Length && i < b.Length && x[i] == y[i]) i++;
                if (i == a.Length || i == b.Length) return a.Length - b.Length;
                return x[i] - y[i];
            }
        }

        // A stackalloc buffer and a fixed string share the frame's shadow stack.
        public static int Reverse(int selection)
        {
            string s = Pick(selection);
            if (s == null) return -1;
            char* copy = stackalloc char[s.Length + 1];
            fixed (char* p = s)
                for (int i = 0; i < s.Length; i++) copy[i] = p[s.Length - 1 - i];
            int hash = 17;
            for (int i = 0; i < s.Length; i++) hash = hash * 31 + copy[i];
            return hash;
        }

        // Each iteration pins again; every copy stays reserved until the frame returns.
        public static int Repeated(int selection)
        {
            string s = Pick(selection);
            int total = 0;
            for (int round = 0; round < 40; round++)
                fixed (char* p = s)
                    total += p == null ? -1 : p[round % (s.Length + 1)];
            return total;
        }

        // A managed exception leaves the fixed statement; the frame still releases its copy,
        // so the caller's next pin starts at the same shadow-stack position.
        public static int Thrown(int selection)
        {
            int first;
            try { first = ThrowOnFirst(Pick(selection)); }
            catch (NullReferenceException) { first = -7; }
            return first * 100 + ThrowOnFirst("Z");
        }

        private static int ThrowOnFirst(string s)
        {
            int[] missing = null;
            fixed (char* p = s)
            {
                if (p != null && p[0] == 'W') return missing[0];
                return p == null ? -1 : p[0] + 1;
            }
        }

        // The frame's copy is not written back: ECMA-334 makes writes through a
        // fixed string undefined. Only reads are compared.
        public static int ReadAfterCall(int selection)
        {
            string s = Pick(selection);
            if (s == null) return -1;
            int before, after;
            fixed (char* p = s)
            {
                before = p[0];
                int measured = Scan(p);
                after = p[0] + measured;
            }
            return before * 1000 + after;
        }

        // Still diagnosed: pinned arrays and pinned element addresses (managed locations, #271/#273).
        public static int RejectArray(char[] values)
        {
            fixed (char* p = values)
                return p == null ? -1 : p[0];
        }

        public static int RejectElement(char[] values)
        {
            fixed (char* p = &values[0])
                return *p;
        }
    }
}
