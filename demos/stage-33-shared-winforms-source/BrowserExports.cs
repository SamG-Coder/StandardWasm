using ProfileExample;
using System.Windows.Forms;

namespace ProfileBrowser
{
    public static class BrowserExports
    {
        public static ProfileState CreateState() => ProfileHandlers.Create();
        public static Form CreateForm() => new Form();
        public static TextBox CreateInput() => new TextBox();
        public static Label CreatePreview() => new Label();
        public static Button CreateClearButton() => new Button();

        public static void Initialize(ProfileState state, Form form, TextBox input, Label preview, Button clear) =>
            ProfileApplication.Initialize(state, form, input, preview, clear);

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
        public static string ReadPreview(Label preview) => preview.Text;
        public static string ReadButton(Button button) => button.Text;
        public static int ReadLimit(TextBox input) => input.MaxLength;
        public static int TakeFocusRequest(TextBox input) => input.TakeFocusRequest();

        public static int InputBound(TextBox input, int index) => input.Bound(index);
        public static int PreviewBound(Label preview, int index) => preview.Bound(index);
        public static int ButtonBound(Button button, int index) => button.Bound(index);
        public static int ClientWidth() => ProfileHandlers.ClientWidth();
        public static int ClientHeight() => ProfileHandlers.ClientHeight();
        public static string InputLabel() => ProfileHandlers.InputLabel();
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
            var form = BrowserExports.CreateForm();
            var input = BrowserExports.CreateInput();
            var preview = BrowserExports.CreatePreview();
            var button = BrowserExports.CreateClearButton();
            BrowserExports.Initialize(state, form, input, preview, button);
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
