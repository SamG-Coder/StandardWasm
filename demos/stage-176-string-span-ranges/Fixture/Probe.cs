using System;
using System.Runtime.CompilerServices;

namespace StringSpanRanges
{
    public static class Probe
    {
        private static int order;

        public static int SuffixCodeUnits()
        {
            string original = "A😀éZ";
            ReadOnlySpan<char> suffix = Tail(original, 2);
            return suffix.Length == 3 && suffix[0] == '\ude00' &&
                suffix[1] == 'é' && suffix[2] == 'Z' ? 347 : 0;
        }

        public static int CopyFromRange()
        {
            string original = "prefix😀éZtail";
            ReadOnlySpan<char> window = Window(original, 7, 3);
            char[] output = new char[3];
            Span<char> destination = new Span<char>(output);
            window.CopyTo(destination);
            ReadOnlySpan<char> ending = window.Slice(1);
            return window.Length == 3 && output[0] == '\ude00' && output[1] == 'é' &&
                output[2] == 'Z' && ending.Length == 2 && ending[0] == 'é' && ending[1] == 'Z' ? 349 : 0;
        }

        public static int EmptyRanges()
        {
            string noText = null;
            string text = "abc";
            ReadOnlySpan<char> nullTail = Tail(noText, 0);
            ReadOnlySpan<char> nullWindow = Window(noText, 0, 0);
            ReadOnlySpan<char> end = Tail(text, 3);
            ReadOnlySpan<char> interiorEmpty = Window(text, 1, 0);
            return nullTail.IsEmpty && nullWindow.IsEmpty && end.IsEmpty && interiorEmpty.IsEmpty &&
                noText.AsSpan().IsEmpty ? 353 : 0;
        }

        public static int RangeFailures()
        {
            bool suffix = TailLength(null, 1) == -1 && TailLength("abc", int.MinValue) == -1 &&
                TailLength("abc", 4) == -1;
            bool range = WindowLength(null, 0, 1) == -1 && WindowLength(null, 1, 0) == -1 &&
                WindowLength("abc", -1, 0) == -1 && WindowLength("abc", 1, -1) == -1 &&
                WindowLength("abc", 2, 2) == -1 && WindowLength("abc", int.MaxValue, int.MaxValue) == -1;
            return suffix && range ? 359 : 0;
        }

        public static int EvaluationOrder()
        {
            order = 0;
            try
            {
                return Source().AsSpan(Start(), Length()).Length;
            }
            catch (ArgumentOutOfRangeException)
            {
                return order == 123 ? 373 : 0;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ReadOnlySpan<char> Tail(string text, int start) => text.AsSpan(start);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ReadOnlySpan<char> Window(string text, int start, int length) => text.AsSpan(start, length);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int TailLength(string text, int start)
        {
            try { return Tail(text, start).Length; }
            catch (ArgumentOutOfRangeException) { return -1; }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int WindowLength(string text, int start, int length)
        {
            try { return Window(text, start, length).Length; }
            catch (ArgumentOutOfRangeException) { return -1; }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string Source()
        {
            order = order * 10 + 1;
            return null;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int Start()
        {
            order = order * 10 + 2;
            return 1;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int Length()
        {
            order = order * 10 + 3;
            return 0;
        }
    }
}
