using System.Drawing;
using System.Windows.Forms;

namespace CounterExample;

internal class CounterForm : Form
{
    internal readonly Button Increment = new() { Name = "increment" };
    internal readonly Label Result = new() { Name = "result" };
    internal Button? Additional { get; private set; }

    private readonly ControlTree layout;
    private readonly CounterState state;
    private readonly Func<int> click;

    internal CounterForm()
    {
        Name = "counter";
        Text = CounterHandlers.Title();
        layout = CounterHandlers.BuildTree();
        state = CounterHandlers.Create(0);
        click = CounterHandlers.Bind(state);
        ClientSize = new Size(CounterHandlers.ClientWidth(layout), CounterHandlers.ClientHeight(layout));
        MinimumSize = SizeFromClientSize(ClientSize);
        Result.Font = new Font("Segoe UI", 24, FontStyle.Bold);
        Result.Text = CounterHandlers.LabelText(state);
        Increment.Text = CounterHandlers.Caption(CounterLayout.Increment);
        Increment.Click += Activate;
        ApplyLayout();
        AcceptButton = Increment;
        ClientSizeChanged += ResizeLayout;
    }

    private void ResizeLayout(object? sender, EventArgs args)
    {
        CounterHandlers.Resize(layout, ClientSize.Width, ClientSize.Height);
        ApplyLayout();
    }

    private void Activate(object? sender, EventArgs args)
    {
        CounterHandlers.Dispatch(click, layout);
        Result.Text = CounterHandlers.LabelText(state);
        ApplyLayout();
    }

    private void ApplyLayout()
    {
        bool hasAdditional = false;
        for (int index = 0; index < CounterHandlers.ControlCount(layout); index++)
        {
            int key = CounterHandlers.ControlValue(layout, index, ControlTree.Key);
            Control control;
            if (key == CounterLayout.Additional)
            {
                hasAdditional = true;
                if (Additional == null)
                {
                    Additional = new Button { Name = "additional", Text = CounterHandlers.Caption(key) };
                    Additional.Click += Activate;
                }
                control = Additional;
            }
            else
            {
                control = key == CounterLayout.Increment ? Increment : Result;
            }
            control.Bounds = new Rectangle(
                CounterHandlers.ControlValue(layout, index, ControlTree.X),
                CounterHandlers.ControlValue(layout, index, ControlTree.Y),
                CounterHandlers.ControlValue(layout, index, ControlTree.Width),
                CounterHandlers.ControlValue(layout, index, ControlTree.Height));
            if (!Controls.Contains(control))
                Controls.Add(control);
        }
        if (!hasAdditional && Additional is { } removed)
        {
            if (ActiveControl == removed)
                ActiveControl = Increment;
            Controls.Remove(removed);
            removed.Dispose();
            Additional = null;
        }
    }
}
