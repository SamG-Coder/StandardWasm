using System.Globalization;

namespace CounterExample;

internal partial class CounterForm
{
    partial void Bind(Button button, Label label, Func<CounterState, Func<int>> bind, string prefix)
    {
        var state = CounterHandlers.Create(0);
        var click = bind(state);
        label.Font = new Font("Segoe UI", 24, FontStyle.Bold);
        label.Text = prefix + CounterHandlers.Read(state).ToString(CultureInfo.InvariantCulture);
        button.Click += (_, _) =>
        {
            label.Text = prefix + CounterHandlers.Dispatch(click).ToString(CultureInfo.InvariantCulture);
        };
    }
}
