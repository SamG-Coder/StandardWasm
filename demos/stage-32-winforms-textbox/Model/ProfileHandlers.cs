namespace ProfileExample
{
    public sealed class ProfileState
    {
        private string name;

        public void SetName(string value) => name = value;
        public string Preview() => string.Concat("Hello, ", name);
    }

    public static class ProfileHandlers
    {
        public const int Input = 1, PreviewBox = 2, ClearButton = 3;

        public static ProfileState Create() => new ProfileState();
        public static void Edit(ProfileState state, string value) => state.SetName(value);
        public static void EditChars(ProfileState state, char[] units) => Edit(state, new string(units));
        public static string Preview(ProfileState state) => state.Preview();
        public static char[] CreateChars(int length) => new char[length];
        public static void SetCodeUnit(char[] units, int index, int code) => units[index] = unchecked((char)code);
        public static int TextLength(string value) => value.Length;
        public static int TextCodeUnit(string value, int index) => value[index];

        public static string Title() => "Profile";
        public static string InputLabel() => "Name";
        public static string ClearLabel() => "Clear";
        public static int InputLimit() => 40;
        public static int ClientWidth() => 420;
        public static int ClientHeight() => 238;
        public static int Left(int control) => control == ClearButton ? 290 : 32;
        public static int Top(int control) => control == PreviewBox ? 116 : control == Input ? 54 : 53;
        public static int Width(int control) => control == PreviewBox ? 356 : control == Input ? 238 : 98;
        public static int Height(int control) => control == PreviewBox ? 90 : 34;
    }

    public static class ProfileCases
    {
        public static string StoreAndRead(int edit)
        {
            var profile = ProfileHandlers.Create();
            var other = ProfileHandlers.Create();
            ProfileHandlers.Edit(other, "Independent");
            if (edit == 1)
                ProfileHandlers.Edit(profile, "Zo\u00eb \U0001f680");
            else if (edit == 2)
            {
                ProfileHandlers.Edit(profile, "Earlier");
                ProfileHandlers.Edit(profile, "");
            }
            else if (edit == 3)
            {
                var units = ProfileHandlers.CreateChars(2);
                ProfileHandlers.SetCodeUnit(units, 0, 0xd83d);
                ProfileHandlers.SetCodeUnit(units, 1, 0xde80);
                ProfileHandlers.EditChars(profile, units);
                ProfileHandlers.SetCodeUnit(units, 0, 65);
            }
            else if (edit == 4) ProfileHandlers.Edit(profile, null);
            else if (edit == 5) ProfileHandlers.Edit(profile, "Cafe\u0301");
            return string.Concat(ProfileHandlers.Preview(profile), string.Concat(" / ", ProfileHandlers.Preview(other)));
        }
    }
}
