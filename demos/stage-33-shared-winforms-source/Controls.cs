namespace System.Windows.Forms
{
    // Browser implementations of the WinForms members used by ProfileApplication.
    // DOM event delivery and rendering stay in the host.
    public sealed class Form
    {
        public string Text { get; set; } = "";
    }

    public sealed class TextBox
    {
        private readonly int[] bounds = new int[4];
        private int focusRequested;

        public string Text { get; set; } = "";
        public int MaxLength { get; set; }

        public void SetBounds(int x, int y, int width, int height) =>
            ControlBounds.Set(bounds, x, y, width, height);

        internal int Bound(int index) => bounds[index];

        public bool Focus()
        {
            focusRequested = 1;
            return true;
        }

        internal int TakeFocusRequest()
        {
            int requested = focusRequested;
            focusRequested = 0;
            return requested;
        }
    }

    public sealed class Label
    {
        private readonly int[] bounds = new int[4];
        public string Text { get; set; } = "";

        public void SetBounds(int x, int y, int width, int height) =>
            ControlBounds.Set(bounds, x, y, width, height);

        internal int Bound(int index) => bounds[index];
    }

    public sealed class Button
    {
        private readonly int[] bounds = new int[4];
        public string Text { get; set; } = "";

        public void SetBounds(int x, int y, int width, int height) =>
            ControlBounds.Set(bounds, x, y, width, height);

        internal int Bound(int index) => bounds[index];
    }

    internal static class ControlBounds
    {
        internal static void Set(int[] bounds, int x, int y, int width, int height)
        {
            bounds[0] = x;
            bounds[1] = y;
            bounds[2] = width;
            bounds[3] = height;
        }
    }
}
