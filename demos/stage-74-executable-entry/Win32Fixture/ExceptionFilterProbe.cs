using System;

public static class ExceptionFilterProbe
{
    public static int MatchPair(int scenario)
    {
        int result = 0;
        try
        {
            try
            {
                if (scenario == 0) return 7 / scenario;
                if (scenario == 1) System.Threading.Monitor.Enter(null);
                if (scenario == 2) return checked(int.MaxValue + scenario);
                return scenario;
            }
            catch (Exception error) when (error is DivideByZeroException or ArgumentNullException)
            {
                result = 12;
            }
            catch (OverflowException)
            {
                result = 27;
            }
        }
        finally
        {
            result += 1;
        }
        return result;
    }
}
