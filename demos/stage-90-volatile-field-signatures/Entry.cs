using VolatileFieldDependency;

namespace VolatileFieldDemo
{
    public sealed class Session
    {
        public volatile int Marker;
    }

    public static class Kernels
    {
        public static int Run(int value)
        {
            var mailbox = new Mailbox();
            var session = new Session();
            session.Marker = value;
            mailbox.State = session.Marker;
            mailbox.Payload = session;
            int first = mailbox.State;
            Update(mailbox, value);
            if (mailbox.Payload != session) return -1;
            return unchecked(first * 3 + mailbox.State + mailbox.Revision);
        }

        private static void Update(Mailbox mailbox, int value)
        {
            mailbox.Revision = unchecked((short)value);
            mailbox.State = unchecked(value + 7);
        }

        public static int Read(Mailbox mailbox) => mailbox.State;
        public static int ReadStatic() => Mailbox.Shared;
    }

    public static class Demo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] image, int index, int value) => image[index] = unchecked((byte)value);
        public static int ReadByte(byte[] image, int index) => image[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int CompileBundle(byte[] bundle, byte[] entries, byte[] output) =>
            PortableCompiler.MethodCompiler.CompileBundle(bundle, entries, output);
    }
}

namespace System.Runtime.Extensions
{
    // Keep the real facade name in the metadata string heap so the verifier
    // can exercise a same-name marker scoped to the wrong platform facade.
    internal sealed class ModifierScopeFixture { }
}
