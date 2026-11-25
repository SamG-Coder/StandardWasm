using System.Drawing;

namespace System.Windows.Forms;

// Build-time compatibility surface used to capture the supported WinForms layout.
internal abstract class Control
{
    public string Name { get; set; } = "";
    public string Text { get; set; } = "";
    public Rectangle Bounds { get; set; }
}

internal class Form : Control
{
    public Size ClientSize { get; set; }
    public List<Control> Controls { get; } = new();
    public Button? AcceptButton { get; set; }
}

internal sealed class Button : Control { }
internal sealed class Label : Control { }
