using System;

namespace ValueConstructionDemo
{
    public static class GenericArrayProbe
    {
        private static T[] Allocate<T>(int length) => new T[length];

        private static T Swap<T>(T[] items, int index, T next)
        {
            T previous = items[index];
            items[index] = next;
            return previous;
        }

        public static int CheckGenericArrayElements(int input)
        {
            var numbers = Allocate<int>(1);
            var signed = Allocate<sbyte>(1);
            var floating = Allocate<double>(1);
            var references = Allocate<string>(1);
            numbers[0] = input;
            signed[0] = -117;
            floating[0] = -2.5;
            references[0] = "before";

            int previous = Swap(numbers, 0, 29);
            int narrow = Swap(signed, 0, (sbyte)127);
            double scalar = Swap(floating, 0, 9.25);
            string text = Swap(references, 0, "after");
            if (scalar != -2.5 || floating[0] != 9.25) return -101;
            return unchecked(previous + numbers[0] * 100 + narrow + signed[0] * 10 +
                text.Length + references[0].Length);
        }

        public static int CheckGenericArrayFailures(int input)
        {
            var items = new int[1];
            try { return Swap(items, input < 0 ? -1 : 1, 29); }
            catch (IndexOutOfRangeException) { }
            try { return Swap<int>(null, 0, 29); }
            catch (NullReferenceException) { return 17; }
        }

        public static int CheckArrayCopy(int input)
        {
            var numbers = new[] { 10, 20, 30, 40 };
            try
            {
                if (input == int.MinValue) Array.Copy(numbers, numbers, -1);
                else if (input < 0) Array.Copy(numbers, null, 1);
                else if (input == 0)
                {
                    Array.Copy(numbers, 0, numbers, 1, 3);
                    Array.Copy(numbers, 2, numbers, 0, 2);
                }
                else if (input == 5) Array.Copy(numbers, 2, numbers, 0, 3);
                else
                {
                    var source = new[] { "red", "green" };
                    var destination = new string[2];
                    Array.Copy(source, destination, 2);
                    return ReferenceEquals(source[1], destination[1]) ? destination[0].Length + 50 : -1;
                }
                return numbers[0] + numbers[1] * 100;
            }
            catch (ArgumentNullException) { return 11; }
            catch (ArgumentOutOfRangeException) { return 13; }
            catch (ArgumentException) { return numbers[0] + 19; }
        }
    }
}
