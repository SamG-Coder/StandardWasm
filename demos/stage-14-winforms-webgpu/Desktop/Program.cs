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
        form.Increment.PerformClick();
        string clicked = form.Result.Text;
        if (!form.PressEnter())
            throw new InvalidOperationException("WinForms did not handle the default Enter key.");
        string entered = form.Result.Text;
        if (initial != "Count: 0" || clicked != "Count: 1" || entered != "Count: 2")
            throw new InvalidOperationException($"Unexpected WinForms event sequence: {initial}, {clicked}, {entered}");
        using var image = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
        image.Save(output);
        Console.WriteLine(JsonSerializer.Serialize(new { initial, clicked, entered }));
    }

    private sealed class TestForm : CounterForm
    {
        internal bool PressEnter() => ProcessDialogKey(Keys.Enter);
    }
}
