namespace SwitchLibrary
{
    public static class Dispatch
    {
        public static int Classify(int value)
        {
            switch (value)
            {
                case -4:
                case -3: return 17;
                case -1: return -29;
                case 0: return 8;
                case 1: return 14;
                case 2: return 3;
                default: return 99;
            }
        }

        public static int Walk(int state, int steps)
        {
            int hash = 17;
            while (steps-- > 0)
            {
                switch (state)
                {
                    case 0: hash = unchecked(hash * 13 + 1); state = 3; break;
                    case 1: hash ^= 91; state = 4; break;
                    case 2: hash -= 31; goto case 0;
                    case 3: hash += 19; state = 1; break;
                    case 4: hash -= 8; state = 2; break;
                    default: hash = ~hash; state = 0; break;
                }
            }
            return hash ^ state;
        }

        public static long AddChoice(long seed, int choice) => unchecked(seed + (choice switch
        {
            -1 => 0x80000000L,
            0 => -1L,
            1 => long.MinValue,
            2 => 0L,
            3 => long.MaxValue,
            _ => 73L
        }));
    }
}
