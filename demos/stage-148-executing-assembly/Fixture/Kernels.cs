using System.Reflection;
using ExecutingDependency;

namespace ExecutingFixture
{
    public static class Kernels
    {
        public static int Own()
        {
            object current = Assembly.GetExecutingAssembly();
            return current == (object)typeof(Kernels).Assembly ? 1 : 0;
        }

        public static int ImportedIdentity()
        {
            object current = Imported.Current();
            int own = current == (object)typeof(Imported).Assembly ? 1 : 0;
            int caller = current == (object)Assembly.GetExecutingAssembly() ? 2 : 0;
            return own | caller;
        }

        public static int Pick(int index)
        {
            object selected = index == 0 ? Assembly.GetExecutingAssembly() : Imported.Current();
            return selected == (object)typeof(Kernels).Assembly ? 7 : 11;
        }
    }
}
