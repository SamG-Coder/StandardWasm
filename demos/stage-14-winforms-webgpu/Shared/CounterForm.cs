using System.Drawing;
using System.Windows.Forms;

namespace CounterExample;

internal partial class CounterForm : Form
{
    internal readonly Button Increment = new() { Name = "increment", Text = "Add one" };
    internal readonly Label Result = new() { Name = "result" };

    internal CounterForm()
    {
        Name = "counter";
        Text = "Counter";
        ClientSize = new Size(420, 240);
        Increment.Bounds = new Rectangle(32, 151, 156, 48);
        Result.Bounds = new Rectangle(32, 35, 350, 94);
        Controls.Add(Result);
        Controls.Add(Increment);
        AcceptButton = Increment;
        Bind(Increment, Result, CounterHandlers.Advance, "Count: ");
    }

    partial void Bind(Button button, Label label, Func<int, int> click, string prefix);
}
