/*
    StellarisHandler.cs - Game handler for Stellaris
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

using RPCParadox2.Memory;

namespace RPCParadox2.GameHandlers;

internal sealed class StellarisHandler : IGameHandler
{
    private const string DISCORD_APP_ID = "1426478074278580318";
    private const string LARGE_IMAGE = "stellaris";
    private const string? SMALL_IMAGE = null;

    private readonly StellarisMemoryScanner _scanner;
    private readonly CancellationTokenSource _cts = new();
    private readonly Thread _updateThread;
    private readonly object _lock = new();
    
    private string _statusLine1 = "Exploring the Galaxy";
    private string _statusLine2 = "";
    private volatile bool _disposed;

    public string DiscordAppId => DISCORD_APP_ID;
    public string GameName => "Stellaris";
    public string LargeImageKey => LARGE_IMAGE;
    public string? SmallImageKey => SMALL_IMAGE;

    public StellarisHandler()
    {
        _scanner = new StellarisMemoryScanner();
        _updateThread = new Thread(UpdateGameDataLoop)
        {
            Name = "StellarisHandlerUpdate",
            IsBackground = true
        };
        _updateThread.Start();
    }

    private void UpdateGameDataLoop()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            string? gameDate = _scanner.GetGameDate();

            lock (_lock)
            {
                if (gameDate != null)
                {
                    _statusLine1 = "In Game";
                    _statusLine2 = $"Date: {gameDate}";
                }
                else
                {
                    _statusLine1 = "In the Main Menu";
                    _statusLine2 = "";
                }
            }

            try
            {
                Task.Delay(5000, _cts.Token).Wait();
            }
            catch (AggregateException)
            {
                break;
            }
        }
    }

    public (string Line1, string Line2) GetStatus()
    {
        lock (_lock)
        {
            return (_statusLine1, _statusLine2);
        }
    }

    public bool IsRunning => _scanner.IsProcessRunning;

    public void Dispose()
    {
        if (_disposed)
            return;
            
        _disposed = true;
        
        _cts.Cancel();
        
        if (_updateThread.IsAlive)
        {
            _updateThread.Join(3000);
        }
        
        _scanner.Dispose();
        _cts.Dispose();
        
        Console.WriteLine("[StellarisHandler] Disposed");
    }
}