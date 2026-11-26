namespace EmptyModuleConsumer
{
    public static class Kernels
    {
        public static int AddOneAfterTwice(int value) => unchecked(EmptyModuleStartup.Kernels.Twice(value) + 1);
    }
}
