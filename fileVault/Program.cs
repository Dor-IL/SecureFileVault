namespace fileVault
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) =>
                MessageBox.Show($"Unexpected error: {e.Exception.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                MessageBox.Show($"Fatal error: {(e.ExceptionObject as Exception)?.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

            ApplicationConfiguration.Initialize();
            Application.Run(new LoginRegister());
        }
    }
}
