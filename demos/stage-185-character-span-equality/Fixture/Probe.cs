using System;

namespace CharacterSpanEquality
{
    public static unsafe class Probe
    {
        public static int SameManagedAnchor()
        {
            char[] data = { 'a', 'a', 'a' };
            var first = new Span<char>(data);
            var second = new Span<char>(data);
            return first.Slice(1, 1) == second.Slice(1, 1) &&
                first.Slice(0, 1) != second.Slice(1, 1) &&
                first.Slice(1, 1) != second.Slice(1, 2) ? 547 : 0;
        }

        public static int DifferentOwners()
        {
            char[] first = { 'x' };
            char[] second = { 'x' };
            var a = new Span<char>(first);
            var b = new Span<char>(second);
            ReadOnlySpan<char> left = a, right = b;
            return left != right ? 557 : 0;
        }

        public static int EmptyAnchors()
        {
            ReadOnlySpan<char> empty = default;
            ReadOnlySpan<char> nil = ((string)null).AsSpan();
            ReadOnlySpan<char> text = "".AsSpan();
            ReadOnlySpan<char> ending = "ab".AsSpan(2);
            var array = View(new char[0]);
            ReadOnlySpan<char> owned = array;
            return empty == nil && empty != text && empty != ending && empty != owned &&
                ending == "ab".AsSpan(2) && owned == array.Slice(0, 0) ? 563 : 0;
        }

        public static int NativeAnchors()
        {
            char* data = stackalloc char[3];
            var whole = new Span<char>(data, 3);
            var shifted = NativeView(data + 1, 2);
            var nullPointer = NativeView((void*)0, 0);
            var emptyNative = new Span<char>(data, 0);
            ReadOnlySpan<char> a = whole.Slice(1), b = shifted;
            return a == b && whole.Slice(2, 0) != emptyNative &&
                nullPointer == default && emptyNative != default ? 569 : 0;
        }

        public static int WrittenArguments()
        {
            char[] letters = { 'x', 'y' };
            var span = new Span<char>(letters);
            return Select(span, 'a') == Select(span, 'b') && letters[0] == 'b' ? 571 : 0;
        }

        private static Span<char> View(char[] data) => new Span<char>(data);

        private static Span<char> NativeView(void* data, int length) => new Span<char>(data, length);

        private static ReadOnlySpan<char> Select(Span<char> view, char value)
        {
            view[0] = value;
            return view;
        }
    }
}
