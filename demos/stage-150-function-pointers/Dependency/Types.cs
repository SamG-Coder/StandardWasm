namespace FunctionPointerDependency
{
    // In a separate assembly, so the fixture reaches these constructors through MemberRefs.
    // NativeSlot's constructor shares the delegate signature (object, native int) but is an
    // ordinary IL constructor; Transform is a real delegate type (Codex review of ee7556a).
    public sealed class NativeSlot
    {
        public readonly object Owner;
        public readonly nint Address;
        public NativeSlot(object owner, nint address)
        {
            Owner = owner;
            Address = address;
        }
    }

    public delegate int Transform(int value);
}
