namespace System.Windows.Forms
{
    // Browser implementations of the WinForms members used by ProfileApplication.
    // DOM event delivery and rendering stay in the host.
    public sealed class Form
    {
        private readonly ControlCollection controls = new ControlCollection();
        public string Text { get; set; } = "";
        public ControlCollection Controls => controls;
    }

    // A linked collection retains typed child references, including multiple
    // children of one type, in insertion order.
    public sealed class ControlCollection
    {
        private ControlEntry first, last;
        private int count;

        public void Add(TextBox value)
        {
            var entry = new ControlEntry();
            entry.Kind = 1;
            entry.Input = value;
            Append(entry);
        }

        public void Add(Label value)
        {
            var entry = new ControlEntry();
            entry.Kind = 2;
            entry.Label = value;
            Append(entry);
        }

        public void Add(Button value)
        {
            var entry = new ControlEntry();
            entry.Kind = 3;
            entry.Button = value;
            Append(entry);
        }

        private void Append(ControlEntry entry)
        {
            if (last == null)
                first = entry;
            else
                last.Next = entry;
            last = entry;
            count++;
        }

        internal int Count() => count;

        internal ControlEntry First() => first;
    }

    public sealed class ControlEntry
    {
        internal ControlEntry Next;
        internal int Kind;
        internal TextBox Input;
        internal Label Label;
        internal Button Button;

        internal string Name()
        {
            if (Kind == 1) return Input.Name;
            if (Kind == 2) return Label.Name;
            return Button.Name;
        }
    }

    public sealed class TextBox
    {
        private readonly int[] bounds = new int[4];
        private int focusRequested;

        public string Name { get; set; } = "";
        public string Text { get; set; } = "";
        public int MaxLength { get; set; }
        public bool AutoSize { set { } } // This host uses the explicitly supplied bounds.

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
        public string Name { get; set; } = "";
        public string Text { get; set; } = "";

        public void SetBounds(int x, int y, int width, int height) =>
            ControlBounds.Set(bounds, x, y, width, height);

        internal int Bound(int index) => bounds[index];
    }

    public sealed class Button
    {
        private readonly int[] bounds = new int[4];
        public string Name { get; set; } = "";
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
