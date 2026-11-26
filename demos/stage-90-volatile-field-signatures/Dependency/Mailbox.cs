namespace VolatileFieldDependency
{
    public sealed class Mailbox
    {
        public volatile int State;
        public volatile short Revision;
        public volatile object Payload;
        public static volatile int Shared;
    }
}
