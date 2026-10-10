public static class RallySplitScreenCheck
{
    public static string Run()
    {
        RallySplitScreenVerification.Run();
        return "Started split-screen Play verification; report: Logs/split-screen-test.txt";
    }
}
