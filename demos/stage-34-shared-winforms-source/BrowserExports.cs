using ProfileExample;
using System.Windows.Forms;

namespace ProfileBrowser
{
    public static class BrowserExports
    {
        public static ProfileState CreateState() => ProfileHandlers.Create();
        public static Form CreateForm(ProfileState state) => ProfileApplication.Create(state);
        public static int ChildCount(Form form) => form.Controls.Count();
        public static ControlEntry FirstChild(Form form) => form.Controls.First();
        public static ControlEntry NextChild(ControlEntry child) => child.Next;
        public static string ChildName(ControlEntry child) => child.Name();
        public static TextBox GetInput(ControlEntry child) => child.Input;
        public static Label GetLabel(ControlEntry child) => child.Label;
        public static Button GetButton(ControlEntry child) => child.Button;

        public static void Edit(ProfileState state, TextBox input, Label preview, char[] units)
        {
            input.Text = new string(units);
            ProfileApplication.NameChanged(state, input, preview);
        }

        public static void Clear(ProfileState state, TextBox input, Label preview)
        {
            ProfileApplication.Clear(input);
            ProfileApplication.NameChanged(state, input, preview);
        }

        public static string ReadTitle(Form form) => form.Text;
        public static string ReadInput(TextBox input) => input.Text;
        public static string ReadLabel(Label label) => label.Text;
        public static string ReadButton(Button button) => button.Text;
        public static int ReadLimit(TextBox input) => input.MaxLength;
        public static int TakeFocusRequest(TextBox input) => input.TakeFocusRequest();

        public static int InputBound(TextBox input, int index) => input.Bound(index);
        public static int LabelBound(Label label, int index) => label.Bound(index);
        public static int ButtonBound(Button button, int index) => button.Bound(index);
        public static int ClientWidth() => ProfileHandlers.ClientWidth();
        public static int ClientHeight() => ProfileHandlers.ClientHeight();
        public static char[] CreateChars(int count) => ProfileHandlers.CreateChars(count);
        public static void SetCodeUnit(char[] units, int index, int value) => ProfileHandlers.SetCodeUnit(units, index, value);
        public static int TextLength(string value) => ProfileHandlers.TextLength(value);
        public static int TextCodeUnit(string value, int index) => ProfileHandlers.TextCodeUnit(value, index);
    }

    public static class FormCases
    {
        public static string Run(int action)
        {
            var state = BrowserExports.CreateState();
            var form = BrowserExports.CreateForm(state);
            var first = BrowserExports.FirstChild(form);
            var second = BrowserExports.NextChild(first);
            var third = BrowserExports.NextChild(second);
            var input = BrowserExports.GetInput(first);
            var preview = BrowserExports.GetLabel(second);
            var button = BrowserExports.GetButton(third);
            if (action == 5)
            {
                var last = BrowserExports.NextChild(third);
                if (BrowserExports.NextChild(last) != null) return "";
                var prompt = BrowserExports.GetLabel(last);
                return BrowserExports.ChildCount(form) == 4 &&
                    input == BrowserExports.GetInput(BrowserExports.FirstChild(form)) &&
                    preview != prompt ? prompt.Text : "";
            }
            if (action == 1) return form.Text;
            if (action == 2) return button.Text;
            if (action == 3) return input.MaxLength == 40 && input.Bound(2) == 238 ? "Bounds applied" : "";
            input.Text = "Ada";
            ProfileApplication.NameChanged(state, input, preview);
            if (action == 4)
            {
                BrowserExports.Clear(state, input, preview);
                return BrowserExports.TakeFocusRequest(input) == 1 && BrowserExports.TakeFocusRequest(input) == 0
                    ? preview.Text : "";
            }
            return preview.Text;
        }
    }
}
