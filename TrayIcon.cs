/*
    TrayIcon.cs - Manages a system tray icon with context menu options.
    Copyright (C) 2026 Derek Li (DairyProducts)

    This program is licensed under the Microsoft Public License (MS-PL).
    See the LICENSE file in the project root for license information.
*/

namespace RPCParadox;

/// <summary>
/// Manages a system tray icon with context menu options.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private readonly Action _onClose;
    private bool _disposed;

    public TrayIcon(Action onClose)
    {
        _onClose = onClose;
    }

    /// <summary>
    /// Initializes and shows the system tray icon.
    /// </summary>
    public void Show()
    {
        var contextMenu = new ContextMenuStrip();

        contextMenu.Items.Add("Info", null, (_, _) =>
        {
            ShowInfoDialog();
        });

        contextMenu.Items.Add(new ToolStripSeparator());

        contextMenu.Items.Add("Close", null, (_, _) =>
        {
            _onClose();
        });

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadIconFromResource(),
            Text = "RPCParadox",
            Visible = true,
            ContextMenuStrip = contextMenu
        };

        _notifyIcon.DoubleClick += (_, _) =>
        {
            ShowInfoDialog();
        };
    }

    /// <summary>
    /// Shows a desktop toast notification from the tray icon.
    /// </summary>
    internal void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.BalloonTipTitle = title;
            _notifyIcon.BalloonTipText = message;
            _notifyIcon.BalloonTipIcon = icon;
            _notifyIcon.ShowBalloonTip(3000);
        }
    }

    private static void ShowInfoDialog()
    {
        string details = $"RPCParadox\nDiscord Rich Presence for Paradox games\n\n";
        string versionInfo = AppConstants.CommitHash != null
            ? $"Version {AppConstants.Version}\nCommit {AppConstants.CommitHash}"
            : $"Version {AppConstants.Version}";
        if (AppConstants.DebugBuild)
        {
            versionInfo += "\nDevelopment/Prerelease Build!";
        }
        MessageBox.Show(
            details + versionInfo,
            "RPCParadox",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private static Icon LoadIconFromResource()
    {
        try
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var resourceName = "RPCParadox.assets.icon.ico";
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                return new Icon(stream);
            }
        }
        catch
        {
            Console.WriteLine("[Tray] Tray icon failed to load");
        }
        return SystemIcons.Application;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        Console.WriteLine("[Tray] Disposed");
    }
}
