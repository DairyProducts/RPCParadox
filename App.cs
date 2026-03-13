/*
    App.cs - Main entry point for the RPCParadox application.
    Copyright (C) 2026 Derek Li (DairyProducts)

    This program is licensed under the Microsoft Public License (MS-PL).
    See the LICENSE file in the project root for license information.
*/

namespace RPCParadox;

internal static class App
{
    private static RichPresence? _richPresence;
    private static TrayIcon? _trayIcon;
    private static int _cleanupCalled = 0;

    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        Console.WriteLine("RPCParadox - Discord Rich Presence for Paradox Games");
        Console.WriteLine("Press Ctrl+C to exit...\n");

        _trayIcon = new TrayIcon(onClose: () => Application.Exit());
        _trayIcon.Show();

        _richPresence = new RichPresence(onNotify: (title, message) =>
        {
            _trayIcon?.ShowNotification(title, message);
        });

        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("\nShutting down...");
            Application.Exit();
        };

        AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
        {
            Cleanup();
        };

        _richPresence.Start();

        _trayIcon.ShowNotification("RPCParadox", "Started running in the background");

        Application.Run();

        Cleanup();
    }

    private static void Cleanup()
    {
        if (Interlocked.Exchange(ref _cleanupCalled, 1) == 1)
            return;

        _trayIcon?.Dispose();
        _trayIcon = null;

        _richPresence?.Dispose();
        _richPresence = null;
    }
}
