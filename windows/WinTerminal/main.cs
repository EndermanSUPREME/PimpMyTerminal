namespace WinTerminal;

static class Program
{
    static System.Threading.Mutex singleton = new Mutex(true, "PimpMyWinTerminal:37f2014212a3a860223c952205fe3546c409c0442b381584cc5d891d44656a10");

    static void Main()
    {
        // usage of mutex ensures we do not run multiple instances of this application
        if (!singleton.WaitOne(TimeSpan.Zero, true))
        {
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new WinTerminalForm());
    }
}