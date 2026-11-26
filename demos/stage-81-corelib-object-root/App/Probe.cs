using System;

namespace CoreRootProbe
{
    public sealed class RemoteValue : MarshalByRefObject
    {
        private int value;

        public RemoteValue(int value) => this.value = value;

        public int Read() => value;
    }

    public static class Entry
    {
        public static int Run(int value)
        {
            var child = new RemoteValue(value);
            object root = child;
            return root == child ? child.Read() : -1;
        }
    }
}
