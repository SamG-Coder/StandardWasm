using System;
using StandardWasm.Managed;

namespace CounterExample
{
    public sealed class CounterState
    {
        private int count;

        public CounterState(int initial) => count = initial;

        public int Advance()
        {
            count = unchecked(count + 1);
            return count;
        }

        public int Value() => count;
    }

    public static class CounterHandlers
    {
        public static CounterState Create(int initial) => new CounterState(initial);
        public static Func<int> Bind(CounterState state) => state.Advance;
        public static int Dispatch(Func<int> callback, ControlTree tree)
        {
            int count = callback();
            CounterLayout.Update(tree, count);
            return count;
        }

        public static string Title() => CounterText.Title;
        public static string Caption(int key) =>
            key == CounterLayout.Additional ? CounterText.Additional : CounterText.Increment;
        public static string LabelText(CounterState state) =>
            string.Concat(CounterText.ResultPrefix, InvariantText.Int32(state.Value()));
        public static int TextLength(string text) => text.Length;
        public static int TextCodeUnit(string text, int index) => text[index];

        public static ControlTree BuildTree() => CounterLayout.Build();
        public static int ClientWidth(ControlTree tree) => tree.ClientWidth();
        public static int ClientHeight(ControlTree tree) => tree.ClientHeight();
        public static int DefaultButton(ControlTree tree) => tree.DefaultButton();
        public static int ControlCapacity(ControlTree tree) => tree.Capacity();
        public static int ControlCount(ControlTree tree) => tree.Count();
        public static int ControlValue(ControlTree tree, int index, int column) => tree.Value(index, column);
        public static int Read(CounterState state) => state.Value();
    }
}
