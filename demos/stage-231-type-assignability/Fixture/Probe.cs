using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace TypeAssignability
{
    public interface IRoot { }
    public interface IInherited : IRoot { }
    public class Base : IInherited { }
    public sealed class Derived : Base { }
    public sealed class Unrelated { }
    public sealed class ClosedSource<T> : Base { }
    public sealed class NeverConstructed : Base { }
    public interface IOpen<T> { }

    public static class Probe
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool InterfaceAccepts(Type value) => typeof(IRoot).IsAssignableFrom(value);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool BaseAccepts(Type value) => typeof(Base).IsAssignableFrom(value);

        public static int DynamicInterface()
        {
            Type positive = new Derived().GetType();
            Type negative = new Unrelated().GetType();
            return InterfaceAccepts(positive) && !InterfaceAccepts(negative) && !InterfaceAccepts(null) ? 701 : 0;
        }

        public static int DynamicBase()
        {
            Type derived = new Derived().GetType();
            Type unrelated = new Unrelated().GetType();
            return BaseAccepts(derived) && !BaseAccepts(unrelated) ? 709 : 0;
        }

        public static int LiteralDescendant() =>
            typeof(IRoot).IsAssignableFrom(typeof(NeverConstructed)) &&
            typeof(IRoot).IsAssignableFrom(typeof(IInherited)) ? 719 : 0;

        public static int ClosedSourceTypes() =>
            typeof(IRoot).IsAssignableFrom(typeof(ClosedSource<int>)) &&
            typeof(Base).IsAssignableFrom(typeof(ClosedSource<string>)) ? 723 : 0;

        public static int IdentityAndNull()
        {
            return typeof(IRoot).IsAssignableFrom(typeof(IRoot)) &&
                !typeof(IRoot).IsAssignableFrom(null) ? 727 : 0;
        }

        public static int ArrayDiagnostic() => typeof(IRoot).IsAssignableFrom(typeof(int[])) ? 0 : 733;

        public static int NullableDiagnostic() => typeof(int?).IsAssignableFrom(typeof(int)) ? 739 : 0;

        public static int OpenTypeDiagnostic() => typeof(IOpen<>).IsAssignableFrom(typeof(IOpen<int>)) ? 0 : 743;

        public static int VariantTargetDiagnostic() =>
            typeof(IEnumerable<object>).IsAssignableFrom(typeof(IEnumerable<string>)) ? 751 : 0;

        public static int UnprovedReceiverDiagnostic() =>
            ViaReceiver(typeof(IRoot), new Derived().GetType()) ? 757 : 0;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ViaReceiver(Type receiver, Type value) => receiver.IsAssignableFrom(value);
    }
}
