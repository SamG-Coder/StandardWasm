using System.Globalization;

namespace CounterExample;

internal partial class CounterForm
{
    partial void Bind(Button button, Label label, Func<int, int> click, string prefix)
    {
        int count = 0;
        label.Font = new Font("Segoe UI", 24, FontStyle.Bold);
        label.Text = prefix + count.ToString(CultureInfo.InvariantCulture);
        button.Click += (_, _) =>
        {
            count = click(count);
            label.Text = prefix + count.ToString(CultureInfo.InvariantCulture);
        };
    }
}
