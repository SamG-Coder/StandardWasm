namespace StandardWasm.Managed
{
    public static class InvariantText
    {
        public static string Int32(int value)
        {
            bool negative = value < 0;
            // Unsigned magnitude also represents Int32.MinValue without overflow.
            uint magnitude = negative ? unchecked(0u - (uint)value) : (uint)value;
            int length = negative ? 2 : 1;
            for (uint remaining = magnitude; remaining >= 10; remaining /= 10)
                length++;

            var digits = new char[length];
            int index = length;
            do
            {
                index--;
                digits[index] = (char)('0' + magnitude % 10);
                magnitude /= 10;
            }
            while (magnitude != 0);
            if (negative)
                digits[0] = '-';
            return new string(digits);
        }
    }
}
