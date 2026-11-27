using PeLibrary;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class DelegateVariance
    {
        internal static bool Assignable(int[] objects, int source, int target, int depth = 0)
        {
            if (depth >= ObjectLayouts.MaxTypeDepth) return false;
            int family = DelegateShapes.Family(objects, source);
            if ((family != DelegateShapes.PlatformFunc && family != DelegateShapes.PlatformAction) ||
                family != DelegateShapes.Family(objects, target)) return false;
            int parameters = DelegateShapes.InvokeParameterCount(objects, source);
            if (parameters < 0 || parameters != DelegateShapes.InvokeParameterCount(objects, target)) return false;
            // Contravariance requires every argument accepted by the view to be
            // accepted by the actual delegate. The result flows the other way.
            for (int i = 0; i < parameters; i++)
                if (!ArgumentAssignable(objects, target, source, i, depth)) return false;
            return family == DelegateShapes.PlatformAction || ArgumentAssignable(objects, source, target, parameters, depth);
        }

        private static bool ArgumentAssignable(int[] objects, int source, int target, int index, int depth)
        {
            int from = DelegateShapes.NominalArgument(objects, source, index);
            int to = DelegateShapes.NominalArgument(objects, target, index);
            if (from == to) return true;
            from = ObjectLayouts.SignatureStorage(objects, from);
            to = ObjectLayouts.SignatureStorage(objects, to);
            if (!IsReference(from) || !IsReference(to) ||
                ObjectLayouts.IsValueType(objects, from) || ObjectLayouts.IsValueType(objects, to)) return false;
            return DelegateShapes.IsKey(from) && DelegateShapes.IsKey(to)
                ? Assignable(objects, from, to, depth + 1) : SignatureTypes.Assignable(from, to, objects);
        }
    }
}
