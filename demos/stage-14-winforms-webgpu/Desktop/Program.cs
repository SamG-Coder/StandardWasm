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
        var sequence = new List<string>();
        var positions = new List<int>();
        var counts = new List<int>();
        var retained = form.Increment;
        void Record()
        {
            sequence.Add(form.Result.Text);
            positions.Add(form.Result.Left);
            counts.Add(form.Controls.Count);
            if (!form.Controls.Contains(retained) || !form.Controls.Contains(form.Result))
                throw new InvalidOperationException("A stable native control was removed.");
        }

        Record();
        form.Increment.PerformClick();
        Record();
        var first = form.Additional ?? throw new InvalidOperationException("The managed tree did not add a button.");
        if (first.Text != "Add one again" || first.Bounds != new Rectangle(204, 151, 184, 48))
            throw new InvalidOperationException("The native button does not match its managed caption and bounds.");

        form.ActiveControl = first;
        if (!form.PressEnter())
            throw new InvalidOperationException("WinForms did not handle Enter.");
        Record();
        if (!first.IsDisposed || form.ActiveControl != retained)
            throw new InvalidOperationException("Removing the focused native button did not restore focus.");

        form.Increment.PerformClick();
        Record();
        var second = form.Additional ?? throw new InvalidOperationException("The managed tree did not restore the button.");
        if (first == second)
            throw new InvalidOperationException("The native host reused a disposed control.");
        form.ActiveControl = second;
        second.PerformClick();
        Record();
        if (!second.IsDisposed || form.ActiveControl != retained)
            throw new InvalidOperationException("The second native removal did not restore focus.");

        if (!sequence.SequenceEqual(new[] { "Count: 0", "Count: 1", "Count: 2", "Count: 3", "Count: 4" }) ||
            !positions.SequenceEqual(new[] { 32, 56, 32, 56, 32 }) ||
            !counts.SequenceEqual(new[] { 2, 3, 2, 3, 2 }))
            throw new InvalidOperationException("The native event sequence did not match the managed layout.");

        using var image = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
        image.Save(output);

        var resized = new List<object>();
        static int[] Bounds(Control control) => [control.Left, control.Top, control.Width, control.Height];
        void RecordResize() => resized.Add(new
        {
            client = new[] { form.ClientSize.Width, form.ClientSize.Height },
            label = Bounds(form.Result), increment = Bounds(form.Increment),
            additional = form.Additional is { } extra ? Bounds(extra) : Array.Empty<int>(),
            caption = form.Result.Text
        });

        form.ClientSize = new Size(520, 300);
        RecordResize();
        form.Increment.PerformClick();
        var resizedButton = form.Additional ?? throw new InvalidOperationException("Resize lost the additional button.");
        form.ActiveControl = resizedButton;
        RecordResize();
        form.ClientSize = new Size(480, 280);
        if (form.ActiveControl != resizedButton || form.Additional != resizedButton)
            throw new InvalidOperationException("Resizing replaced the focused native control.");
        RecordResize();
        resizedButton.PerformClick();
        if (!resizedButton.IsDisposed || form.ActiveControl != retained)
            throw new InvalidOperationException("Removal after resize did not return native focus.");
        RecordResize();
        form.ClientSize = new Size(420, 240);
        RecordResize();
        if (form.Increment != retained || form.Increment.Bounds != new Rectangle(32, 151, 156, 48) ||
            form.Result.Bounds != new Rectangle(32, 35, 350, 94))
            throw new InvalidOperationException("The resize round trip did not restore the original native bounds.");

        string evidence = JsonSerializer.Serialize(new { sequence, labelLeft = positions, controlCounts = counts,
            dynamicBounds = new[] { 204, 151, 184, 48 }, resized,
            width = form.ClientSize.Width, height = form.ClientSize.Height });
        File.WriteAllText(Path.ChangeExtension(output, ".json"), evidence);
        Console.WriteLine(evidence);
    }

    private sealed class TestForm : CounterForm
    {
        internal bool PressEnter() => ProcessDialogKey(Keys.Enter);
    }
}
