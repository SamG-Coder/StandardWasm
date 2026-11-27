extern alias Foreign;

using System;
using System.Reflection;
using UserAssembly = Foreign::System.Reflection.Assembly;

namespace RuntimeAssemblyArrays
{
    public static class Probe
    {
        private static Assembly[] Descriptors(Assembly second)
        {
            var result = new Assembly[3];
            result[0] = Assembly.GetExecutingAssembly();
            result[1] = second;
            return result;
        }

        public static int DescriptorElements()
        {
            Assembly core = typeof(int).Assembly;
            Assembly[] values = Descriptors(core);
            return values.Length == 3 && values[0] == typeof(Probe).Assembly &&
                values[1] == core && values[0] != values[1] && values[2] == null ? 179 : 0;
        }

        private static UserAssembly[] UserValues()
        {
            var result = new UserAssembly[1];
            result[0] = new UserAssembly(181);
            return result;
        }

        public static int ForeignName() => UserValues()[0].Value;

        private static int ObjectLength(object[] values) => values.Length;

        public static int CovarianceBoundary() => ObjectLength(new Assembly[183]);

        public static int OriginalAppDomain() => AppDomain.CurrentDomain.GetAssemblies().Length > 0 ? 191 : 0;
    }
}
