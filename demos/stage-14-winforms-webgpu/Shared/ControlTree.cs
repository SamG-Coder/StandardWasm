namespace CounterExample
{
    // Each control has the same compact layout in the native and browser hosts.
    public sealed class ControlTree
    {
        public const int Label = 1, Button = 2;
        public const int Key = 0, Kind = 1, X = 2, Y = 3, Width = 4, Height = 5;
        private const int Stride = 6;
        private readonly int[] controls;
        private readonly int defaultButton;
        private int width, height;
        private int count;

        internal ControlTree(int width, int height, int defaultButton, int capacity)
        {
            this.width = width;
            this.height = height;
            this.defaultButton = defaultButton;
            controls = new int[capacity * Stride];
        }

        internal bool Add(int key, int kind, int x, int y, int width, int height)
        {
            if (count >= Capacity() || Find(key) >= 0) return false;
            int start = count * Stride;
            controls[start + Key] = key;
            controls[start + Kind] = kind;
            controls[start + X] = x;
            controls[start + Y] = y;
            controls[start + Width] = width;
            controls[start + Height] = height;
            count++;
            return true;
        }

        internal bool Remove(int key)
        {
            int index = Find(key);
            if (index < 0) return false;
            int limit = (count - 1) * Stride;
            for (int slot = index * Stride; slot < limit; slot++)
                controls[slot] = controls[slot + Stride];
            count--;
            return true;
        }

        private int Find(int key)
        {
            for (int i = 0; i < count; i++)
                if (Value(i, Key) == key) return i;
            return -1;
        }

        internal void Move(int key, int x)
        {
            int index = Find(key);
            if (index >= 0)
                controls[index * Stride + X] = x;
        }

        internal void SetClientSize(int newWidth, int newHeight)
        {
            width = newWidth;
            height = newHeight;
        }

        internal void OffsetBounds(int key, int x, int y, int width)
        {
            int index = Find(key);
            if (index < 0) return;
            int slot = index * Stride;
            controls[slot + X] = controls[slot + X] + x;
            controls[slot + Y] = controls[slot + Y] + y;
            controls[slot + Width] = controls[slot + Width] + width;
        }

        public int Value(int index, int column) => controls[index * Stride + column];
        public int Capacity() => controls.Length / Stride;
        public int Count() => count;
        public int ClientWidth() => width;
        public int ClientHeight() => height;
        public int DefaultButton() => defaultButton;
    }

    public static class CounterLayout
    {
        public const int Result = 1, Increment = 2, Additional = 3;
        internal const int MinimumWidth = 420, MinimumHeight = 240;
        private const int LabelX = 32, ButtonBottomOffset = 89, AdditionalRightOffset = 216;

        public static ControlTree Build()
        {
            var tree = new ControlTree(MinimumWidth, MinimumHeight, Increment, 3);
            tree.Add(Result, ControlTree.Label, LabelX, 35, 350, 94);
            tree.Add(Increment, ControlTree.Button, 32, MinimumHeight - ButtonBottomOffset, 156, 48);
            return tree;
        }

        public static void Resize(ControlTree tree, int width, int height)
        {
            if (width < MinimumWidth) width = MinimumWidth;
            if (height < MinimumHeight) height = MinimumHeight;
            int dx = width - tree.ClientWidth(), dy = height - tree.ClientHeight();
            // Each delta is relative to the previous client size so a resize
            // round trip restores the exact bounds even after a click.
            tree.OffsetBounds(Result, 0, 0, dx);
            tree.OffsetBounds(Increment, 0, dy, 0);
            tree.OffsetBounds(Additional, dx, dy, 0);
            tree.SetClientSize(width, height);
        }

        public static void Update(ControlTree tree, int count)
        {
            int odd = count & 1;
            tree.Move(Result, LabelX + odd * 24);
            if (odd != 0)
                tree.Add(Additional, ControlTree.Button, tree.ClientWidth() - AdditionalRightOffset,
                    tree.ClientHeight() - ButtonBottomOffset, 184, 48);
            else
                tree.Remove(Additional);
        }
    }
}
