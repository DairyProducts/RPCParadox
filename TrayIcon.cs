/*
    TrayIcon.cs - Manages a system tray icon with context menu options.
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
            Icon = SystemIcons.Application,
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
        MessageBox.Show(
            $"RPCParadox\n\nDiscord Rich Presence for Paradox Games.\nVersion {AppConstants.Version}",
            "RPCParadox",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
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
    }
}
