using System.Text.Json;

// Windows oracle for stage 79. Real WinForms records the order in which work runs when an event handler opens a
// modal dialog. The point is the nested message loop: work posted before ShowDialog, work posted by the dialog and a
// timer all run inside the click handler's ShowDialog call, and the handler resumes only after the dialog closes.
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var trace = new List<string>();
        using var main = new Form { Text = "Modal oracle", Opacity = 0, ShowInTaskbar = false };
        var button = new Button { Text = "Open" };
        main.Controls.Add(button);

        button.Click += (_, _) =>
        {
            trace.Add("click-begin");
            main.BeginInvoke(() => trace.Add("posted-during-click"));
            using var dialog = new Form { Text = "Dialog", Opacity = 0, ShowInTaskbar = false };
            var timer = new System.Windows.Forms.Timer { Interval = 10 };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                trace.Add("dialog-timer");
                dialog.Close();
            };
            dialog.Shown += (_, _) =>
            {
                trace.Add("dialog-shown");
                dialog.BeginInvoke(() => trace.Add("posted-in-dialog"));
                timer.Start();
            };
            dialog.ShowDialog(main);
            timer.Dispose();
            trace.Add("click-after-dialog");
            main.BeginInvoke(() => { trace.Add("closing"); main.Close(); });
        };
        main.Shown += (_, _) =>
        {
            trace.Add("main-shown");
            main.BeginInvoke(() => trace.Add("posted-1"));
            main.BeginInvoke(() => button.PerformClick());
            // Posted before the click runs, so the click's nested modal loop dispatches it.
            main.BeginInvoke(() => trace.Add("posted-after-click"));
        };
        Application.Run(main);
        trace.Add("run-returned");
        File.WriteAllText(args.Single(), JsonSerializer.Serialize(trace));
    }
}
