using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ScopedPinnedValues
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Rectangle
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public Rectangle(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }
    }

    public sealed class Envelope
    {
        public Rectangle Item;
    }

    public static class Probe
    {
        private static unsafe int Borrow(ref Rectangle rectangle, int delta, int status)
        {
            fixed (Rectangle* pointer = &rectangle)
                return Update(pointer, delta, status);
        }

        private static unsafe int Update(Rectangle* rectangle, int delta, int status)
        {
            rectangle->Left = rectangle->Left - delta;
            rectangle->Bottom = rectangle->Bottom + delta;
            return status;
        }

        private static unsafe IntPtr Escape(ref Rectangle rectangle)
        {
            fixed (Rectangle* pointer = &rectangle)
                return (IntPtr)pointer;
        }

        public static int WritesEvenOnFailure()
        {
            var value = new Rectangle(-3, 7, 19, 43);
            int status = Borrow(ref value, 2, 0);
            return status == 0 && value.Left == -5 && value.Top == 7 &&
                value.Right == 19 && value.Bottom == 45 ? 101 : -1;
        }

        public static int ArrayElementAlias()
        {
            var values = new[] { new Rectangle(11, 13, 17, 19), new Rectangle(23, 29, 31, 37) };
            ref Rectangle alias = ref values[0];
            int status = Borrow(ref alias, 3, 1);
            return status == 1 && alias.Left == 8 && values[0].Bottom == 22 &&
                values[1].Left == 23 && values[1].Bottom == 37 ? 103 : -1;
        }

        public static int ClassFieldAlias()
        {
            var owner = new Envelope { Item = new Rectangle(11, 13, 17, 19) };
            ref Rectangle alias = ref owner.Item;
            int status = Borrow(ref alias, 5, 1);
            return status == 1 && owner.Item.Left == 6 && alias.Bottom == 24 ? 107 : -1;
        }

        public static int DefaultAndRepeatedBorrow()
        {
            Rectangle value = default;
            int first = Borrow(ref value, 2, 0);
            int second = Borrow(ref value, 3, 1);
            return first == 0 && second == 1 && value.Left == -5 && value.Top == 0 &&
                value.Right == 0 && value.Bottom == 5 ? 109 : -1;
        }

#if NET10_0_OR_GREATER
        public static int TypedNull()
        {
            try { Borrow(ref Unsafe.NullRef<Rectangle>(), 1, 0); }
            catch (NullReferenceException) { return 113; }
            return -1;
        }
#endif

        public static int Escapes()
        {
            Rectangle value = default;
            Escape(ref value);
            return 131;
        }
    }
}
