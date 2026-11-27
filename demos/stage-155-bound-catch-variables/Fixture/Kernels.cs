using System;

namespace BoundCatchFixture
{
    // A library exception class: thrown as a managed object, caught by its own type or a base.
    public sealed class FixtureException : Exception
    {
        public readonly int Code;
        public FixtureException(int code) { Code = code; }
    }

    static class Failing
    {
        public static int Value = Fail();
        static int Fail() => throw new InvalidOperationException();
    }

    // Stage 155 (issue #269, decision 0082): catch handlers that bind the caught exception
    // (`catch (X e)` and then read e). Every kernel returns an int that the x86 CLR also returns.
    public static class Kernels
    {
        static int zero;
        static int[] none;

        // A thrown managed object reaches the handler as itself.
        public static int CaughtNotNull()
        {
            try { throw new InvalidOperationException(); }
            catch (Exception e) { return e == null ? 0 : 1; }
        }

        public static int CaughtHResult()
        {
            try { throw new InvalidOperationException(); }
            catch (Exception e) { return e.HResult; }
        }

        public static int LibraryClass()
        {
            try { throw new FixtureException(42); }
            catch (FixtureException e) { return e.Code; }
        }

        // A base-class clause binds the derived object.
        public static int BaseCatchOfDerived()
        {
            try { throw new ArgumentNullException(); }
            catch (ArgumentException e) { return e is ArgumentNullException ? e.HResult : 0; }
        }

        public static int InnerOfBound()
        {
            try { throw new InvalidOperationException("outer", new FixtureException(7)); }
            catch (InvalidOperationException e) { return e.InnerException is FixtureException inner ? inner.Code : 0; }
        }

        // Runtime-raised exceptions with the CLR's default message become CoreLib objects.
        public static int DivideAsException()
        {
            try { return 1 / zero; }
            catch (Exception e) { return e is DivideByZeroException ? e.HResult : 0; }
        }

        public static int NullAsException()
        {
            try { return none.Length; }
            catch (Exception e) { return e is NullReferenceException ? e.HResult : 0; }
        }

        public static int IndexAsException()
        {
            try { var values = new int[1]; return values[zero + 3]; }
            catch (Exception e) { return e is IndexOutOfRangeException ? e.HResult : 0; }
        }

        public static int OverflowAsException()
        {
            try { return checked(int.MaxValue + (zero + 1)); }
            catch (Exception e) { return e is OverflowException ? e.HResult : 0; }
        }

        // throw; after binding keeps the object: the outer handler sees the same reference.
        public static int RethrowFixedIdentity()
        {
            Exception first = null;
            try
            {
                try { return 1 / zero; }
                catch (DivideByZeroException e) { first = e; throw; }
            }
            catch (Exception outer) { return ReferenceEquals(first, outer) ? 5 : 0; }
        }

        public static int RethrowObjectIdentity()
        {
            Exception first = null;
            try
            {
                try { throw new FixtureException(1); }
                catch (FixtureException e) { first = e; throw; }
            }
            catch (Exception outer) { return ReferenceEquals(first, outer) ? 6 : 0; }
        }

        // A handler nested inside a bound handler binds its own exception; the outer one is unchanged.
        public static int NestedBound()
        {
            try { throw new FixtureException(3); }
            catch (FixtureException outer)
            {
                try { throw new FixtureException(4); }
                catch (FixtureException inner) { return outer.Code * 10 + inner.Code; }
            }
        }

        // Issue #170's TypeInitializationException, now observable from managed code.
        public static int InitializerSameObject()
        {
            TypeInitializationException first = null, second = null;
            try { _ = Failing.Value; } catch (TypeInitializationException e) { first = e; }
            try { _ = Failing.Value; } catch (TypeInitializationException e) { second = e; }
            return first != null && ReferenceEquals(first, second) ? 1 : 0;
        }

        public static int InitializerInner()
        {
            try { return Failing.Value; }
            catch (TypeInitializationException e) { return e.InnerException is InvalidOperationException ? 1 : 0; }
        }

        // A failed cast's CLR message names both types, which the cast site does not supply, so a
        // bound handler would see a different Message: the module stops with an explicit trap.
        public static int CastReachesBoundHandler()
        {
            object value = new ArgumentException();
            try { return ((InvalidOperationException)value).HResult; }
            catch (Exception e) { return e is InvalidCastException ? 7 : 0; }
        }
    }
}
