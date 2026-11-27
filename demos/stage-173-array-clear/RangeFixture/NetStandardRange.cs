using System;

namespace ManagedArrayClear
{
    public static class NetStandardRange
    {
        public static int ClearPacked()
        {
            var values = new ushort[3];
            values[0] = 65312;
            values[1] = 32799;
            values[2] = 1947;
            Array.Clear(values, 1, 1);
            return values[0] + 10 * values[1] + values[2];
        }
    }
}
