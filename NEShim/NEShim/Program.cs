namespace NEShim;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Steam is initialized here so it is available before the window is shown.
        // SteamManager.Initialize() is actually called inside MainForm.InitializeEmulator()
        // after the emulation thread is created so the overlay callback has the thread to target.

        Application.Run(new MainForm());

        // SteamManager.Shutdown() is called from MainForm.OnFormClosing().
    }
}
