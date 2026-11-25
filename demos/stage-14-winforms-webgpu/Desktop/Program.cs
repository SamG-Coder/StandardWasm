using System.Text.Json;
using CounterExample;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Length == 2 && args[0] == "--verify")
        {
            Verify(args[1]);
            return;
        }
        Application.Run(new CounterForm());
    }

    private static void Verify(string output)
    {
        using var form = new TestForm { Opacity = 0, ShowInTaskbar = false };
        form.Show();
        Application.DoEvents();
        string initial = form.Result.Text;
        int leftBefore = form.Result.Left;
        form.Increment.PerformClick();
        string clicked = form.Result.Text;
        int leftClicked = form.Result.Left;
        if (!form.PressEnter())
            throw new InvalidOperationException("WinForms did not handle the default Enter key.");
        string entered = form.Result.Text;
        int leftEntered = form.Result.Left;
        if (initial != "Count: 0" || clicked != "Count: 1" || entered != "Count: 2")
            throw new InvalidOperationException($"Unexpected WinForms event sequence: {initial}, {clicked}, {entered}");
        using var image = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
        image.Save(output);
        if (leftBefore != 32 || leftClicked != 56 || leftEntered != 32 || form.Controls.Count != 2)
            throw new InvalidOperationException("The native form did not apply the managed layout updates.");
        Console.WriteLine(JsonSerializer.Serialize(new { initial, clicked, entered,
            labelLeft = new[] { leftBefore, leftClicked, leftEntered }, controls = form.Controls.Count,
            width = form.ClientSize.Width, height = form.ClientSize.Height }));
    }

    private sealed class TestForm : CounterForm
    {
        internal bool PressEnter() => ProcessDialogKey(Keys.Enter);
    }
}
