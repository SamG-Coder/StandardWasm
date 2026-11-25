using System.Globalization;

namespace CounterExample;

internal partial class CounterForm
{
    partial void Bind(Button button, Label label, Func<CounterState, int> click, string prefix)
    {
        var state = CounterHandlers.Create(0);
        label.Font = new Font("Segoe UI", 24, FontStyle.Bold);
        label.Text = prefix + CounterHandlers.Read(state).ToString(CultureInfo.InvariantCulture);
        button.Click += (_, _) =>
        {
            label.Text = prefix + click(state).ToString(CultureInfo.InvariantCulture);
        };
    }
}
