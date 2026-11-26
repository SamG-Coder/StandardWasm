namespace SwitchDispatch
{
    // Each method below makes Roslyn emit an IL switch instruction (a jump table). Out-of-range values, including
    // negative ones after Roslyn's bias subtraction, must take the default path, as on the CLR.
    public static class Dispatch
    {
        // Dense cases 0..6.
        public static int DayKind(int day)
        {
            switch (day)
            {
                case 0: case 6: return 2;
                case 1: case 2: case 3: case 4: return 1;
                case 5: return 3;
                default: return -1;
            }
        }

        // Negative labels and a gap. Roslyn subtracts the lowest label (-3) and fills the gap with the default.
        public static int Biased(int value)
        {
            switch (value)
            {
                case -3: return 30;
                case -2: return 20;
                case -1: return 10;
                case 1: return 11;
                case 2: return 12;
                default: return 0;
            }
        }

        // A switch expression also lowers to a jump table.
        public static int Weight(int code) => code switch
        {
            10 => 1, 11 => 2, 12 => 4, 13 => 8, 14 => 16, 15 => 32, _ => 0
        };

        // goto case and fallthrough loops: the table targets re-enter one another.
        public static int Collatzish(int start, int steps)
        {
            int value = start, total = 0;
            for (int i = 0; i < steps; i++)
            {
                switch (value & 3)
                {
                    case 0: value >>= 1; total += 1; break;
                    case 1: value = value * 3 + 1; total += 2; goto case 0;
                    case 2: value -= 1; total += 3; break;
                    case 3: value += 5; total += 4; goto case 2;
                }
            }
            return total * 1000 + (value & 255);
        }

        // Ordinary use: a tokenizer state machine over character codes. Counts the numbers, identifiers and
        // operators in the input, and returns -1 on an invalid character.
        public static int Tokenize(int[] text)
        {
            const int Start = 0, InNumber = 1, InIdentifier = 2;
            int state = Start, numbers = 0, identifiers = 0, operators = 0;
            for (int i = 0; i <= text.Length; i++)
            {
                int c = i < text.Length ? text[i] : ' ';
                int kind = c >= '0' && c <= '9' ? 0 : (c >= 'a' && c <= 'z') ? 1 : c == ' ' ? 2 : c == '+' || c == '*' ? 3 : 4;
                switch (state)
                {
                    case Start:
                        switch (kind)
                        {
                            case 0: state = InNumber; break;
                            case 1: state = InIdentifier; break;
                            case 2: break;
                            case 3: operators++; break;
                            default: return -1;
                        }
                        break;
                    case InNumber:
                        if (kind == 0) break;
                        numbers++;
                        state = Start;
                        goto case Start;
                    case InIdentifier:
                        if (kind == 0 || kind == 1) break;
                        identifiers++;
                        state = Start;
                        goto case Start;
                }
            }
            return numbers * 10000 + identifiers * 100 + operators;
        }

        // Builds the tokenizer input inside Wasm (int[] is opaque to JS): "x1 + 42*y + 7" with one character swapped.
        public static int TokenizeSample(int replaceAt, int replacement)
        {
            int[] text = new int[13];
            text[0] = 'x'; text[1] = '1'; text[2] = ' '; text[3] = '+'; text[4] = ' '; text[5] = '4'; text[6] = '2';
            text[7] = '*'; text[8] = 'y'; text[9] = ' '; text[10] = '+'; text[11] = ' '; text[12] = '7';
            if (replaceAt >= 0 && replaceAt < text.Length) text[replaceAt] = replacement;
            return Tokenize(text);
        }

        // A 64-entry table.
        public static int Wide(int index)
        {
            switch (index)
            {
                case 0: return 7; case 1: return 14; case 2: return 21; case 3: return 28; case 4: return 35;
                case 5: return 42; case 6: return 49; case 7: return 56; case 8: return 63; case 9: return 70;
                case 10: return 77; case 11: return 84; case 12: return 91; case 13: return 98; case 14: return 105;
                case 15: return 112; case 16: return 119; case 17: return 126; case 18: return 133; case 19: return 140;
                case 20: return 147; case 21: return 154; case 22: return 161; case 23: return 168; case 24: return 175;
                case 25: return 182; case 26: return 189; case 27: return 196; case 28: return 203; case 29: return 210;
                case 30: return 217; case 31: return 224; case 32: return 231; case 33: return 238; case 34: return 245;
                case 35: return 252; case 36: return 259; case 37: return 266; case 38: return 273; case 39: return 280;
                case 40: return 287; case 41: return 294; case 42: return 301; case 43: return 308; case 44: return 315;
                case 45: return 322; case 46: return 329; case 47: return 336; case 48: return 343; case 49: return 350;
                case 50: return 357; case 51: return 364; case 52: return 371; case 53: return 378; case 54: return 385;
                case 55: return 392; case 56: return 399; case 57: return 406; case 58: return 413; case 59: return 420;
                case 60: return 427; case 61: return 434; case 62: return 441; case 63: return 448;
                default: return -index;
            }
        }
    }
}
