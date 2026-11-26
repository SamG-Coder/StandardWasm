using System;

namespace Shared
{
    // Same namespace and name as the Twin assembly's class, but a distinct CLR type.
    public sealed class NamedException : Exception
    {
    }
}

namespace ExceptionFixture
{
    public class FixtureException : Exception
    {
    }

    public sealed class DerivedException : FixtureException
    {
    }

    public sealed class OtherException : Exception
    {
    }

    // Each kernel returns an int that encodes which handlers ran, in order.
    // Handlers never bind the exception variable (C# emits `pop`), matching the
    // compiler's handler-entry rule. The CLR oracle runs the same DLL.
    public static class Kernels
    {
        private static int trace;

        private static void Throw(Exception value) => throw value;

        private static FixtureException Missing() => null;

        public static int TypedCatch()
        {
            try { Throw(new FixtureException()); return 0; }
            catch (FixtureException) { return 11; }
        }

        public static int DerivedBeforeBase()
        {
            try { Throw(new DerivedException()); return 0; }
            catch (DerivedException) { return 21; }
            catch (FixtureException) { return 22; }
        }

        public static int BaseCatchesDerived()
        {
            try { Throw(new DerivedException()); return 0; }
            catch (FixtureException) { return 31; }
        }

        public static int MismatchReachesOuter()
        {
            try
            {
                try { Throw(new OtherException()); return 0; }
                catch (FixtureException) { return 41; }
            }
            catch (OtherException) { return 42; }
        }

        public static int CatchException()
        {
            try { Throw(new OtherException()); return 0; }
            catch (Exception) { return 51; }
        }

        public static int RethrowKeepsType()
        {
            try
            {
                try { Throw(new DerivedException()); return 0; }
                catch (FixtureException) { throw; }
            }
            catch (DerivedException) { return 61; }
            catch (FixtureException) { return 62; }
        }

        public static int FinallyOrder()
        {
            trace = 0;
            try
            {
                try { trace = trace * 10 + 1; Throw(new FixtureException()); }
                finally { trace = trace * 10 + 2; }
            }
            catch (FixtureException) { trace = trace * 10 + 3; }
            return trace; // 123
        }

        public static int NullVariableThrow()
        {
            // The static type is the linked exception class; the CLR raises NullReferenceException.
            try { throw Missing(); }
            catch (NullReferenceException) { return 71; }
            catch (FixtureException) { return 72; }
        }

        public static int RepeatedHandlerVisit()
        {
            // The same catch (Exception) handler first receives a fixed exception, then an
            // object. Each rethrow must re-raise what entered it, never a stale reference.
            int result = 0, zero = 0;
            for (int i = 0; i < 2; i++)
            {
                try
                {
                    try
                    {
                        if (i == 0) result += 1 / zero;
                        else Throw(new FixtureException());
                    }
                    catch (Exception) { throw; }
                }
                catch (DivideByZeroException) { result += 100; }
                catch (FixtureException) { result += 10; }
            }
            return result; // 110
        }

        public static int SameNameStaysDistinct()
        {
            try
            {
                try { Shared.TwinThrower.Throw(); return 0; }
                catch (Shared.NamedException) { return 81; } // the fixture's own class
            }
            catch (Exception) { return 82; }
        }

        public static int Filtered()
        {
            try { Throw(new FixtureException()); return 0; }
            catch (FixtureException) when (trace >= 0) { return 91; }
        }

        public static int Uncaught()
        {
            Throw(new FixtureException());
            return 0;
        }
    }
}
