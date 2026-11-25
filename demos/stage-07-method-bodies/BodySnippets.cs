namespace BodyLibrary
{
    public static class BodySnippets
    {
        public static int WithHandler(int value)
        {
            try
            {
                return 15 / value;
            }
            catch (System.DivideByZeroException)
            {
                return -1;
            }
        }
    }
}
