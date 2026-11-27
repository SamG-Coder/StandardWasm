using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace MonoInterlockedIncrement
{
    public static class Probe
    {
        private static int counter;

        public static int LocalAlias()
        {
            int value = 6;
            ref int alias = ref value;
            int actual = Interlocked.Increment(ref alias);
            return actual == 7 && value == 7 ? 17 : 0;
        }

        public static int StaticWrap()
        {
            counter = int.MaxValue;
            int first = Interlocked.Increment(ref counter);
            int second = Interlocked.Increment(ref counter);
            return first == int.MinValue && second == int.MinValue + 1 && counter == second ? 23 : 0;
        }

        public static int FieldAndArray()
        {
            var slot = new Slot();
            int[] values = { 19, 41 };
            int field = Interlocked.Increment(ref slot.Value);
            int array = Interlocked.Increment(ref values[1]);
            return field == 1 && slot.Value == 1 && array == 42 && values[1] == 42 && values[0] == 19 ? 29 : 0;
        }

        public static int NullReceiver()
        {
            Slot slot = null;
            try { return Interlocked.Increment(ref slot.Value); }
            catch (NullReferenceException) { return 31; }
        }

        public static int OriginalSchedulerCounter()
        {
            var first = new Scheduler();
            var second = new Scheduler();
            int id = first.Id;
            return id > 0 && first.Id == id && second.Id == id + 1 ? 37 : 0;
        }

        public static int NullLocation()
        {
            try { return Interlocked.Increment(ref Unsafe.NullRef<int>()); }
            catch (NullReferenceException) { return 43; }
        }

        private sealed class Slot { internal int Value; }

        private sealed class Scheduler : TaskScheduler
        {
            protected override IEnumerable<Task> GetScheduledTasks() => null;
            protected override void QueueTask(Task task) { }
            protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => false;
        }
    }
}
