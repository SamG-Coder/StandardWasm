using System;

namespace FrameworkDelegateDependency
{
    // A linked publisher. Its field-like event accessors combine and remove
    // handlers in this assembly, reached through System.Runtime like an app.
    public sealed class Publisher
    {
        public event EventHandler Raised;

        public void Raise() => Raised?.Invoke(this, EventArgs.Empty);
    }
}
