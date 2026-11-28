using System;
using System.Runtime.CompilerServices;

namespace ExplicitTypeInitialization
{
    public static class Probe
    {
        private static int preciseRuns, deferredRuns, genericRuns, recursiveRuns;
        private static int Record(ref int count) => ++count;

        private static class Precise
        {
            static Precise() { preciseRuns++; }
        }

        private static class Deferred
        {
            internal static readonly int Value = Record(ref deferredRuns);
        }

        private sealed class Generic<T>
        {
            static Generic() { genericRuns++; }
        }

        private static class Recursive
        {
            static Recursive()
            {
                recursiveRuns++;
                RuntimeHelpers.RunClassConstructor(typeof(Recursive).TypeHandle);
                recursiveRuns++;
            }
        }

        private static class Failing
        {
            static Failing() { throw new InvalidOperationException(); }
        }

        private sealed class NoInitializer { }

        public static int PreciselyOnce()
        {
            int before = preciseRuns;
            RuntimeHelpers.RunClassConstructor(typeof(Precise).TypeHandle);
            int after = preciseRuns;
            RuntimeHelpers.RunClassConstructor(typeof(Precise).TypeHandle);
            return 100 * before + 10 * after + preciseRuns;
        }

        public static int ForcesBeforeFieldInit()
        {
            RuntimeHelpers.RunClassConstructor(typeof(Deferred).TypeHandle);
            // The fixture observes initialization without reading Deferred.Value.
            return deferredRuns;
        }

        public static int IndependentGenericInitializers()
        {
            RuntimeHelpers.RunClassConstructor(typeof(Generic<int>).TypeHandle);
            int first = genericRuns;
            RuntimeHelpers.RunClassConstructor(typeof(Generic<int>).TypeHandle);
            int repeated = genericRuns;
            RuntimeHelpers.RunClassConstructor(typeof(Generic<string>).TypeHandle);
            return 100 * first + 10 * repeated + genericRuns;
        }

        public static int OpenTypeAndArray()
        {
            RuntimeHelpers.RunClassConstructor(typeof(Generic<>).TypeHandle);
            RuntimeHelpers.RunClassConstructor(typeof(Generic<long>[]).TypeHandle);
            return genericRuns;
        }

        public static int RecursiveInitializer()
        {
            RuntimeHelpers.RunClassConstructor(typeof(Recursive).TypeHandle);
            return recursiveRuns;
        }

        public static int CachedFailure()
        {
            TypeInitializationException first = null, second = null;
            try { RuntimeHelpers.RunClassConstructor(typeof(Failing).TypeHandle); }
            catch (TypeInitializationException error) { first = error; }
            try { RuntimeHelpers.RunClassConstructor(typeof(Failing).TypeHandle); }
            catch (TypeInitializationException error) { second = error; }
            return first != null && ReferenceEquals(first, second) &&
                first.InnerException is InvalidOperationException ? 1 : 0;
        }

        private static void TriggerUnprovedHandle(RuntimeTypeHandle handle)
        {
            RuntimeHelpers.RunClassConstructor(handle);
        }

        public static int UnprovedHandleDiagnostic()
        {
            TriggerUnprovedHandle(typeof(NoInitializer).TypeHandle);
            return 98;
        }

        public static int AbsentInitializer()
        {
            RuntimeHelpers.RunClassConstructor(typeof(NoInitializer).TypeHandle);
            return 97;
        }
    }
}
