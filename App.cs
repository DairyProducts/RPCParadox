/*
    App.cs - Main entry point for the RPCParadox application.
    Copyright (C) 2026 Derek Li (DairyProducts)

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

namespace RPCParadox2;

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

        Console.WriteLine("RPCParadox2 - Discord Rich Presence for Paradox Games");
        Console.WriteLine("Press Ctrl+C to exit...\n");

        _richPresence = new RichPresence();

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

        _trayIcon = new TrayIcon(onClose: () => Application.Exit());
        _trayIcon.Show();

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
