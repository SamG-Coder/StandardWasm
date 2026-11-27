using System;
using System.Collections.Generic;

namespace ResourceStringFixture
{
    // Stage 154 (issue #304, decision 0081): each parameterless CoreLib exception constructor
    // takes its default message from an SR property, `get_X => SR.GetResourceString("X")`. The
    // compiler folds those keys from CoreLib's embedded neutral resources, so these
    // constructors compile without ResourceManager.
    public static class Kernels
    {
        public static int InvalidOperation()
        {
            try { throw new InvalidOperationException(); }
            catch (InvalidOperationException) { return 1; }
        }

        public static int ArgumentNull()
        {
            try { throw new ArgumentNullException(); }
            catch (ArgumentException) { return 2; }
        }

        public static int NotSupported()
        {
            try { throw new NotSupportedException(); }
            catch (NotSupportedException) { return 3; }
        }

        public static int Format()
        {
            try { throw new FormatException(); }
            catch (SystemException) { return 4; }
        }

        public static int KeyNotFound()
        {
            try { throw new KeyNotFoundException(); }
            catch (KeyNotFoundException) { return 5; }
        }

        public static int Overflow()
        {
            try { throw new OverflowException(); }
            catch (ArithmeticException) { return 6; }
        }

        public static int NotImplemented()
        {
            try { throw new NotImplementedException(); }
            catch (Exception) { return 7; }
        }

        // Several in sequence, each caught by a different handler kind.
        public static int Sequence()
        {
            int score = 0;
            try { throw new InvalidOperationException(); } catch (InvalidOperationException) { score += 1; }
            try { throw new ArgumentNullException(); } catch (ArgumentException) { score += 10; }
            try { throw new NotSupportedException(); } catch (Exception) { score += 100; }
            return score;
        }
    }
}
