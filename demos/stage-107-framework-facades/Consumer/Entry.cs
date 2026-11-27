using System.Collections.Generic;

namespace FacadeConsumer
{
    public static class Entry
    {
        public static int IsNaN(double value) => double.IsNaN(value) ? 1 : 0;
        public static int IsHighSurrogate(int value) => char.IsHighSurrogate((char)value) ? 1 : 0;

        public static int List()
        {
            var list = new List<int>();
            list.Add(5);
            return list[0];
        }

        public static int ListGrowth(int seed)
        {
            var list = new List<int>();
            for (int index = 0; index < 11; index++)
                list.Add(unchecked(seed + index));
            return unchecked(list[0] + list[10] + list.Count * 1000);
        }
    }
}
